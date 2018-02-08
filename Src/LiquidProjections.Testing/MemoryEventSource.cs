using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LiquidProjections.Abstractions;

namespace LiquidProjections.Testing
{
    /// <summary>
    /// An event source which stores all the transactions in memory and has methods which are convenient for testing.
    /// </summary>
    public class MemoryEventSource
    {
        private readonly int batchSize;
        private readonly List<MemorySubscription> subscriptions = new List<MemorySubscription>();
        private readonly List<Transaction> history = new List<Transaction>();
        private long lastHistoryCheckpoint;
        private TaskCompletionSource<bool> historyGrowthTaskCompletionSource = new TaskCompletionSource<bool>();
        private readonly object syncRoot = new object();

        /// <summary>
        /// Creates a new instance of the event source.
        /// </summary>
        /// <param name="batchSize">
        /// The maximum number of transactions in batches which are handled by the subscribers. The default is 10.
        /// </param>
        public MemoryEventSource(int batchSize = 10)
        {
            this.batchSize = batchSize;
        }

        /// <summary>
        /// Creates a new subscription which will handle all the transactions after the given checkpoint.
        /// Waits (synchronously) for the subscription to process all the transactions that are already in the event source.
        /// If the given <paramref name="lastProcessedCheckpoint"/> is ahead of the event source
        /// and that is ignored by the handler,
        /// waits (synchronously) for a replacement subscription to be created
        /// and to process all the transactions that are already in the event source.
        /// The replacement subscription is not returned.
        /// The code that creates the replacement subscription is responsible for cancelling it.
        /// </summary>
        /// <param name="lastProcessedCheckpoint">
        /// If has value, only the transactions with checkpoints greater than the given value will be processed.
        /// </param>
        /// <param name="subscriber">The <see cref="Subscriber"/> which will handle the transactions.</param>
        /// <param name="subscriptionId">An arbitrary string identifying the subscription.</param>
        /// <returns>
        /// An object implementing the <see cref="IDisposable"/> interface.
        /// Disposing the object will cancel the subscription asynchronously.
        /// </returns>
        public IDisposable Subscribe(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            return SubscribeAsync(lastProcessedCheckpoint, subscriber, subscriptionId)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Creates a new subscription which will handle all the transactions after the given checkpoint.
        /// Waits asynchronously for the subscription to process all the transactions that are already in the event source.
        /// If the given <paramref name="lastProcessedCheckpoint"/> is ahead of the event source
        /// and that is ignored by the handler,
        /// waits asynchronously for a replacement subscription to be created
        /// and to process all the transactions that are already in the event source.
        /// The replacement subscription is not returned.
        /// The code that creates the replacement subscription is responsible for cancelling it.
        /// </summary>
        /// <param name="lastProcessedCheckpoint">
        /// If has value, only the transactions with checkpoints greater than the given value will be processed.
        /// </param>
        /// <param name="subscriber">The <see cref="Subscriber"/> which will handle the transactions.</param>
        /// <param name="subscriptionId">An arbitrary string identifying the subscription.</param>
        /// <returns>
        /// A task that returns an object implementing the <see cref="IDisposable"/> interface.
        /// Disposing the object will cancel the subscription asynchronously.
        /// </returns>
        public async Task<IDisposable> SubscribeAsync(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            MemorySubscription subscription = SubscribeWithoutWaitingInternal(lastProcessedCheckpoint, subscriber, subscriptionId);

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

        /// <summary>
        /// Creates a new subscription which will handle all the transactions after the given checkpoint.
        /// Does not wait for the subscription to process any transactions.
        /// If the given <paramref name="lastProcessedCheckpoint"/> is ahead of the event source
        /// and that is ignored by the handler,
        /// does not wait for a replacement subscription to be created.
        /// </summary>
        /// <param name="lastProcessedCheckpoint">
        /// If has value, only the transactions with checkpoints greater than the given value will be processed.
        /// </param>
        /// <param name="subscriber">The <see cref="Subscriber"/> which will handle the transactions.</param>
        /// <param name="subscriptionId">An arbitrary string identifying the subscription.</param>
        /// <returns>
        /// An object implementing the <see cref="IDisposable"/> interface.
        /// Disposing the object will cancel the subscription asynchronously.
        /// </returns>
        public IDisposable SubscribeWithoutWaiting(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            return SubscribeWithoutWaitingInternal(lastProcessedCheckpoint, subscriber, subscriptionId);
        }

        private MemorySubscription SubscribeWithoutWaitingInternal(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            var subscription = new MemorySubscription(lastProcessedCheckpoint ?? 0, batchSize, subscriber, subscriptionId, this);

            lock (syncRoot)
            {
                subscriptions.Add(subscription);
            }

            subscription.Start();
            return subscription;
        }

        /// <summary>
        /// Adds a new transaction containing the given events to the end of the event source.
        /// An incremental checkpoint number is automatically generated for the transaction.
        /// Waits for all the subscriptions to process the transaction.
        /// </summary>
        /// <param name="events">The events to be included into the transaction.</param>
        /// <returns>A task returning the created transaction.</returns>
        public async Task<Transaction> Write(params object[] events)
        {
            Transaction transaction = WriteWithoutWaiting(events);
            await WaitForAllSubscriptions().ConfigureAwait(false);
            return transaction;
        }

        /// <summary>
        /// Adds a new transaction containing the given events to the end of the event source.
        /// An incremental checkpoint number is automatically generated for the transaction.
        /// Does not wait for the transaction to be processed by any subscriptions.
        /// </summary>
        /// <param name="events">The events to be included into the transaction.</param>
        /// <returns>A task returning the created transaction.</returns>
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
        
        /// <summary>
        /// Adds the given transactions to the end of the event source.
        /// If a transaction has <c>-1</c> instead of a valid checkpoint number,
        /// an incremental checkpoint number is automatically generated for the transaction.
        /// Waits for all the subscriptions to process the transactions.
        /// </summary>
        /// <param name="transactions">The transactions to be added.</param>
        /// <returns>A task that completes after all the subscriptions have processed the transactions.</returns>
        public Task Write(params Transaction[] transactions)
        {
            WriteWithoutWaiting(transactions);
            return WaitForAllSubscriptions();
        }

        /// <summary>
        /// Adds the given transactions to the end of the event source.
        /// If a transaction has <c>-1</c> instead of a valid checkpoint number,
        /// an incremental checkpoint number is automatically generated for the transaction.
        /// Does not wait for the transaction to be processed by any subscriptions.
        /// </summary>
        /// <param name="transactions">The transactions to be added.</param>
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

        /// <summary>
        /// Adds a new transaction containing the given event to the end of the event source.
        /// Allows to specify headers for the transaction.
        /// An incremental checkpoint number is automatically generated for the transaction.
        /// Waits for all the subscriptions to process the transaction.
        /// </summary>
        /// <param name="anEvent">The event to be included into the transaction.</param>
        /// <param name="headers">The headers for the transaction.</param>
        /// <returns>A task returning the created transaction.</returns>
        public async Task<Transaction> WriteWithHeaders(object anEvent, IDictionary<string, object> headers)
        {
            Transaction transaction = WriteWithHeadersWithoutWaiting(anEvent, headers);
            await WaitForAllSubscriptions().ConfigureAwait(false);
            return transaction;
        }

        /// <summary>
        /// Adds a new transaction containing the given event to the end of the event source.
        /// Allows to specify headers for the transaction.
        /// An incremental checkpoint number is automatically generated for the transaction.
        /// Does not wait for the transaction to be processed by any subscriptions.
        /// </summary>
        /// <param name="anEvent">The event to be included into the transaction.</param>
        /// <param name="headers">The headers for the transaction.</param>
        /// <returns>A task returning the created transaction.</returns>
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
        
        /// <summary>
        /// Waits for all the subscriptions to process all the transaction which are already in the event source
        /// but not yet processed by a subscription.
        /// </summary>
        /// <returns>A task that completes after all the subscriptions have processed the transactions.</returns>
        public async Task WaitForAllSubscriptions()
        {
            List<MemorySubscription> subscriptionsAtStart;
            long checkpointAtStart;
            
            lock (syncRoot)
            {
                subscriptionsAtStart = subscriptions.ToList();
                checkpointAtStart = lastHistoryCheckpoint;
            }

            foreach (MemorySubscription subscription in subscriptionsAtStart)
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

        /// <summary>
        /// Checks whether the event source has a non-cancelled subscription with the given identifier.
        /// </summary>
        /// <param name="subscriptionId">
        /// The identifier of the subscription which was specified when the subscription was created.
        /// </param>
        public bool HasSubscriptionForId(string subscriptionId)
        {
            lock (syncRoot)
            {
                MemorySubscription subscription = subscriptions.SingleOrDefault(aSubscription => aSubscription.Id == subscriptionId);
                return (subscription != null) && !subscription.IsDisposed;
            }
        }

        internal bool IsFutureCheckpoint(long checkpoint)
        {
            lock (syncRoot)
            {
                return checkpoint > lastHistoryCheckpoint;
            }
        }

        internal int GetNextTransactionIndex(long checkpoint)
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

        internal Task WaitForNewTransactions()
        {
            lock (syncRoot)
            {
                return historyGrowthTaskCompletionSource.Task;
            }
        }

        internal Transaction[] GetTransactionsFromIndex(int startIndex)
        {
            lock (syncRoot)
            {
                int count = history.Count - startIndex;
                var result = new Transaction[count];
                history.CopyTo(startIndex, result, 0, count);
                return result;
            }
        }
    }
}
