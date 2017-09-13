using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiquidProjections.Abstractions;

namespace LiquidProjections.Testing
{
    public class MemoryEventSource
    {
        private readonly int batchSize;
        private readonly List<Subscription> subscriptions = new List<Subscription>();
        private readonly List<Transaction> history = new List<Transaction>();
        private long lastHistoryCheckpoint;
        private TaskCompletionSource<bool> historyGrowthTaskCompletionSource = new TaskCompletionSource<bool>();
        private readonly object syncRoot = new object();

        public MemoryEventSource(int batchSize = 10)
        {
            this.batchSize = batchSize;
        }

        public IDisposable Subscribe(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            return SubscribeAsync(lastProcessedCheckpoint, subscriber, subscriptionId)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public async Task<IDisposable> SubscribeAsync(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            Subscription subscription = SubscribeWithoutWaitingInternal(lastProcessedCheckpoint, subscriber, subscriptionId);

            try
            {
                await subscription.WaitForCheckingWhetherItIsAhead().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Do nothing.
            }

            long checkpointAtStart;

            lock (syncRoot)
            {
                checkpointAtStart = lastHistoryCheckpoint;
            }

            try
            {
                await subscription.WaitUntilCheckpoint(checkpointAtStart).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Do nothing.
            }

            return subscription;
        }

        public IDisposable SubscribeWithoutWaiting(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            return SubscribeWithoutWaitingInternal(lastProcessedCheckpoint, subscriber, subscriptionId);
        }

        private Subscription SubscribeWithoutWaitingInternal(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            var subscription = new Subscription(lastProcessedCheckpoint ?? 0, batchSize, subscriber, subscriptionId, this);

            lock (syncRoot)
            {
                subscriptions.Add(subscription);
            }

            subscription.Start();
            return subscription;
        }

        public async Task<Transaction> Write(params object[] events)
        {
            Transaction transaction = WriteWithoutWaiting(events);
            await WaitForAllSubscriptions().ConfigureAwait(false);
            return transaction;
        }

        public Transaction WriteWithoutWaiting(params object[] events)
        {
            Transaction transaction = new Transaction
            {
                Events = events.Select(@event => new EventEnvelope
                {
                    Body = @event
                }).ToArray()
            };

            WriteWithoutWaiting(transaction);

            return transaction;
        }
        
        public Task Write(params Transaction[] transactions)
        {
            WriteWithoutWaiting(transactions);
            return WaitForAllSubscriptions();
        }

        public void WriteWithoutWaiting(params Transaction[] transactions)
        {
            if (transactions.Any())
            {
                lock (syncRoot)
                {
                    foreach (Transaction transaction in transactions)
                    {
                        if (transaction.Checkpoint == -1)
                        {
                            lastHistoryCheckpoint++;
                            transaction.Checkpoint = lastHistoryCheckpoint;
                        }
                        else
                        {
                            lastHistoryCheckpoint = transaction.Checkpoint;
                        }

                        if (string.IsNullOrEmpty(transaction.Id))
                        {
                            transaction.Id = transaction.Checkpoint.ToString(CultureInfo.InvariantCulture);
                        }

                        history.Add(transaction);
                    }

                    TaskCompletionSource<bool> oldHistoryGrowthTaskCompletionSource = historyGrowthTaskCompletionSource;
                    historyGrowthTaskCompletionSource = new TaskCompletionSource<bool>();

                    // Execute continuations asynchronously.
                    Task.Run(() => oldHistoryGrowthTaskCompletionSource.SetResult(false));
                }
            }
        }

        public async Task<Transaction> WriteWithHeaders(object anEvent, IDictionary<string, object> headers)
        {
            Transaction transaction = WriteWithHeadersWithoutWaiting(anEvent, headers);
            await WaitForAllSubscriptions().ConfigureAwait(false);
            return transaction;
        }

        public Transaction WriteWithHeadersWithoutWaiting(object anEvent, IDictionary<string, object> headers)
        {
            Transaction transaction = new Transaction
            {
                Events = new[]
                {
                    new EventEnvelope
                    {
                        Body = anEvent,
                        Headers = headers
                    }
                }
            };

            WriteWithoutWaiting(transaction);

            return transaction;
        }
        
        public async Task WaitForAllSubscriptions()
        {
            List<Subscription> subscriptionsAtStart;
            long checkpointAtStart;
            
            lock (syncRoot)
            {
                subscriptionsAtStart = subscriptions.ToList();
                checkpointAtStart = lastHistoryCheckpoint;
            }

            foreach (Subscription subscription in subscriptionsAtStart)
            {
                try
                {
                    await subscription.WaitUntilCheckpoint(checkpointAtStart).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Do nothing.
                }
            }
        }

        public bool HasSubscriptionForId(string subscriptionId)
        {
            lock (syncRoot)
            {
                Subscription subscription = subscriptions.SingleOrDefault(aSubscription => aSubscription.Id == subscriptionId);
                return (subscription != null) && !subscription.IsDisposed;
            }
        }

        private bool IsFutureCheckpoint(long checkpoint)
        {
            lock (syncRoot)
            {
                return checkpoint > lastHistoryCheckpoint;
            }
        }

        private int GetNextTransactionIndex(long checkpoint)
        {
            lock (syncRoot)
            {
                int index = 0;
                
                while (index < history.Count)
                {
                    if (history[index].Checkpoint > checkpoint)
                    {
                        break;
                    }

                    index++;
                }

                return index;
            }
        }

        private Task WaitForNewTransactions()
        {
            lock (syncRoot)
            {
                return historyGrowthTaskCompletionSource.Task;
            }
        }

        private Transaction[] GetTransactionsFromIndex(int startIndex)
        {
            lock (syncRoot)
            {
                int count = history.Count - startIndex;
                var result = new Transaction[count];
                history.CopyTo(startIndex, result, 0, count);
                return result;
            }
        }

        private class Subscription : IDisposable
        {
            private long lastProcessedCheckpoint;
            private readonly int batchSize;
            private readonly Subscriber subscriber;
            private readonly MemoryEventSource memoryEventSource;
            private bool isDisposed;
            private CancellationTokenSource cancellationTokenSource;
            private readonly object syncRoot = new object();
            private Task task;
            private TaskCompletionSource<long> progressCompletionSource = new TaskCompletionSource<long>();
            private readonly TaskCompletionSource<bool> waitForCheckingWhetherItIsAheadCompletionSource = new TaskCompletionSource<bool>();
            
            public Subscription(long lastProcessedCheckpoint, int batchSize,
                Subscriber subscriber, string subscriptionId, MemoryEventSource memoryEventSource)
            {
                this.lastProcessedCheckpoint = lastProcessedCheckpoint;
                this.batchSize = batchSize;
                this.subscriber = subscriber;
                Id = subscriptionId;
                this.memoryEventSource = memoryEventSource;
            }

            public void Start()
            {
                if (task != null)
                {
                    throw new InvalidOperationException("Already started.");
                }

                lock (syncRoot)
                {
                    if (isDisposed)
                    {
                        throw new ObjectDisposedException(nameof(Subscription));
                    }

                    cancellationTokenSource = new CancellationTokenSource();

                    SubscriptionInfo info = new SubscriptionInfo
                    {
                        Id = Id,
                        Subscription = this,
                        CancellationToken = cancellationTokenSource.Token
                    };
                    
                    task = Task.Factory.StartNew(
                            async () =>
                            {
                                try
                                {
                                    await RunAsync(info).ConfigureAwait(false);
                                }
                                catch (Exception)
                                {
                                    Dispose();
                                }
                            },
                            cancellationTokenSource.Token,
                            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                            TaskScheduler.Default)
                        .Unwrap();
                }
            }

            private async Task RunAsync(SubscriptionInfo info)
            {
                long oldLastProcessedCheckpoint;

                lock (syncRoot)
                {
                    oldLastProcessedCheckpoint = lastProcessedCheckpoint;
                }
                
                if (memoryEventSource.IsFutureCheckpoint(oldLastProcessedCheckpoint))
                {
                    await subscriber.NoSuchCheckpoint(info).ConfigureAwait(false);
                }

#pragma warning disable 4014
                // Run continuations asynchronously.
                Task.Run(() => waitForCheckingWhetherItIsAheadCompletionSource.TrySetResult(false));
#pragma warning restore 4014

                int nextTransactionIndex = memoryEventSource.GetNextTransactionIndex(oldLastProcessedCheckpoint);
                
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    Task waitForNewTransactions = memoryEventSource.WaitForNewTransactions();
                    Transaction[] transactions = memoryEventSource.GetTransactionsFromIndex(nextTransactionIndex);
                    
                    Transaction[] requestedTransactions = transactions
                        .Where(transaction => transaction.Checkpoint > oldLastProcessedCheckpoint)
                        .ToArray();
                    
                    foreach (IList<Transaction> batch in requestedTransactions.InBatchesOf(batchSize))
                    {
                        await subscriber.HandleTransactions(new ReadOnlyCollection<Transaction>(batch.ToList()), info)
                            .ConfigureAwait(false);
                    }

                    if (requestedTransactions.Any())
                    {
                        lock (syncRoot)
                        {
                            lastProcessedCheckpoint = requestedTransactions[requestedTransactions.Length - 1].Checkpoint;

                            if (!isDisposed)
                            {
                                TaskCompletionSource<long> oldProgressCompletionSource = progressCompletionSource;
                                progressCompletionSource = new TaskCompletionSource<long>();

#pragma warning disable 4014
                                // Run continuations asynchronously.
                                Task.Run(() => oldProgressCompletionSource.SetResult(lastProcessedCheckpoint));
#pragma warning restore 4014
                            }
                        }
                    }

                    nextTransactionIndex += transactions.Length;
                    
                    await waitForNewTransactions
                        .WithWaitCancellation(cancellationTokenSource.Token)
                        .ConfigureAwait(false);
                }
            }
    
            public void Dispose()
            {
                lock (syncRoot)
                {
                    if (!isDisposed)
                    {
                        isDisposed = true;

                        // Run continuations and wait for the subscription task asynchronously.
                        Task.Run(() =>
                        {
                            progressCompletionSource.SetCanceled();
                            waitForCheckingWhetherItIsAheadCompletionSource.TrySetCanceled();

                            if (cancellationTokenSource != null)
                            {
                                if (!cancellationTokenSource.IsCancellationRequested)
                                {
                                    cancellationTokenSource.Cancel();
                                }

                                task?.Wait();
                                cancellationTokenSource.Dispose();
                            }
                        });
                    }
                }
            }

            public bool IsDisposed
            {
                get
                {
                    lock (syncRoot)
                    {
                        return isDisposed;
                    }
                }
            }
    
            public string Id { get; }

            public async Task WaitUntilCheckpoint(long checkpoint)
            {
                while (true)
                {
                    Task progressTask;

                    lock (syncRoot)
                    {
                        progressTask = progressCompletionSource.Task;
                        
                        if (lastProcessedCheckpoint >= checkpoint)
                        {
                            return;
                        }
                    }

                    await progressTask.ConfigureAwait(false);
                }
            }

            public Task WaitForCheckingWhetherItIsAhead() => waitForCheckingWhetherItIsAheadCompletionSource.Task;
        }
    }
}
