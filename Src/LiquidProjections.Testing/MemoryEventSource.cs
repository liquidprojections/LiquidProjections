using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LiquidProjections.Abstractions;

namespace LiquidProjections.Testing
{
    public class MemoryEventSource
    {
        private readonly int batchSize;
        private long lastCheckpoint;

        private readonly List<Subscription> subscribers = new List<Subscription>();
        private readonly List<Transaction> history = new List<Transaction>();

        public MemoryEventSource(int batchSize = 10)
        {
            this.batchSize = batchSize;
        }

        public IDisposable Subscribe(long? lastProcessedCheckpoint, Subscriber subscriber, string subscriptionId)
        {
            lastCheckpoint = lastProcessedCheckpoint ?? 0;
            var subscription = new Subscription(lastCheckpoint, batchSize, subscriber, subscriptionId);

            subscribers.Add(subscription);

            async Task AsyncAction()
            {
                if (history.LastOrDefault()?.Checkpoint < lastProcessedCheckpoint)
                {
                    await subscriber.NoSuchCheckpoint(new SubscriptionInfo
                    {
                        Id = subscriptionId,
                        Subscription = subscription
                    });
                }

                foreach (Transaction transaction in history)
                {
                    await subscription.Send(new[] {transaction}).ConfigureAwait(false);
                }
            }

            AsyncAction().ConfigureAwait(false).GetAwaiter().GetResult();

            return subscription;
        }


        public async Task<Transaction> Write(params object[] events)
        {
            Transaction transaction = new Transaction
            {
                Events = events.Select(@event => new EventEnvelope
                {
                    Body = @event
                }).ToArray()
            };

            await Write(transaction).ConfigureAwait(false);

            return transaction;
        }

        public async Task Write(params Transaction[] transactions)
        {
            foreach (var transaction in transactions)
            {
                if (transaction.Checkpoint == -1)
                {
                    transaction.Checkpoint = (++lastCheckpoint);
                }
                else
                {
                    lastCheckpoint = transaction.Checkpoint;
                }

                if (string.IsNullOrEmpty(transaction.Id))
                {
                    transaction.Id = transaction.Checkpoint.ToString(CultureInfo.InvariantCulture);
                }

                history.Add(transaction);
            }

            foreach (var subscriber in subscribers)
            {
                await subscriber.Send(transactions).ConfigureAwait(false);
            }
        }

        public async Task<Transaction> WriteWithHeaders(object anEvent, IDictionary<string, object> headers)
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

            await Write(transaction).ConfigureAwait(false);

            return transaction;
        }

        public bool HasSubscriptionForId(string subscriptionId)
        {
            Subscription subscription = subscribers.SingleOrDefault(s => s.Id == subscriptionId);
            return (subscription != null) && !subscription.IsDisposed;
        }
    }

    internal class Subscription : IDisposable
    {
        private readonly long lastProcessedCheckpoint;
        private readonly int batchSize;
        private readonly Subscriber subscriber;
        private readonly string subscriptionId;
        private bool disposed = false;

        public Subscription(long lastProcessedCheckpoint, int batchSize,
            Subscriber subscriber, string subscriptionId)
        {
            this.lastProcessedCheckpoint = lastProcessedCheckpoint;
            this.batchSize = batchSize;
            this.subscriber = subscriber;
            this.subscriptionId = subscriptionId;
        }

        public async Task Send(IEnumerable<Transaction> transactions)
        {
            if (!disposed)
            {
                var subscriptionInfo = new SubscriptionInfo
                {
                    Id = subscriptionId,
                    Subscription = this
                };

                Transaction[] requestedTransactions = transactions.Where(t => t.Checkpoint > lastProcessedCheckpoint).ToArray();
                foreach (var batch in requestedTransactions.InBatchesOf(batchSize))
                {
                    await subscriber.HandleTransactions(new ReadOnlyCollection<Transaction>(batch.ToList()), subscriptionInfo)
                        .ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            disposed = true;
        }

        public bool IsDisposed => disposed;

        public string Id => subscriptionId;
    }
}
