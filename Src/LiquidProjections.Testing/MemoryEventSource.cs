using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LiquidProjections.Testing;

namespace LiquidProjections
{
    public class MemoryEventSource
    {
        private readonly int batchSize;
        private long lastCheckpoint;

        private readonly List<Subscriber> subscribers = new List<Subscriber>();
        private readonly List<Transaction> history = new List<Transaction>();

        public MemoryEventSource(int batchSize = 10)
        {
            this.batchSize = batchSize;
        }

        public IDisposable Subscribe(long? lastProcessedCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler, string subscriptionId)
        {
            lastCheckpoint = lastProcessedCheckpoint ?? 0;
            var subscriber = new Subscriber(lastCheckpoint, batchSize, handler);

            subscribers.Add(subscriber);

            Func<Task> asyncAction = async () =>
            {
                foreach (Transaction transaction in history)
                {
                    await subscriber.Send(new[] { transaction }).ConfigureAwait(false);
                }
            };

            asyncAction().ConfigureAwait(false).GetAwaiter().GetResult();

            return subscriber;
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
    }

    internal class Subscriber : IDisposable
    {
        private readonly long lastProcessedCheckpoint;
        private readonly int batchSize;
        private readonly Func<IReadOnlyList<Transaction>, Task> handler;
        private bool disposed = false;
        
        public Subscriber(long lastProcessedCheckpoint, int batchSize, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            this.lastProcessedCheckpoint = lastProcessedCheckpoint;
            this.batchSize = batchSize;
            this.handler = handler;
        }

        public async Task Send(IEnumerable<Transaction> transactions)
        {
            if (!disposed)
            {
                foreach (var batch in transactions.Where(t => t.Checkpoint > lastProcessedCheckpoint).InBatchesOf(batchSize))
                {
                    await handler(new ReadOnlyCollection<Transaction>(batch.ToList())).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            disposed = true;
        }
    }
}
