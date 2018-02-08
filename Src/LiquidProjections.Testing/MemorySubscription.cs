using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiquidProjections.Abstractions;

namespace LiquidProjections.Testing
{
    public class MemorySubscription : IDisposable
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

        private readonly TaskCompletionSource<bool> waitForCheckingWhetherItIsAheadCompletionSource =
            new TaskCompletionSource<bool>();

        public MemorySubscription(long lastProcessedCheckpoint, int batchSize,
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
                    throw new ObjectDisposedException(nameof(MemorySubscription));
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
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                        TaskScheduler.Default)
                    .Unwrap();
            }
        }

        private async Task RunAsync(SubscriptionInfo info)
        {
            if (IsDisposed)
            {
                return;
            }

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

            while (!IsDisposed)
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

                    progressCompletionSource.SetCanceled();
                    waitForCheckingWhetherItIsAheadCompletionSource.TrySetCanceled();

                    if (cancellationTokenSource != null)
                    {
                        try
                        {
                            cancellationTokenSource.Cancel();
                        }
                        catch (AggregateException)
                        {
                            // Ignore.
                        }

                        if (task == null)
                        {
                            cancellationTokenSource.Dispose();
                        }
                        else
                        {
                            // Run continuations and wait for the subscription task asynchronously.
                            task.ContinueWith(_ => cancellationTokenSource.Dispose());
                        }
                    }
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