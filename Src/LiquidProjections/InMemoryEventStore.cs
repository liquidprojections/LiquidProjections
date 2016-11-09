using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class MemoryEventSource : IEventStore
    {
        private readonly int batchSize;
        private long lastCheckpoint;

        private readonly List<Subscriber> subscribers = new List<Subscriber>();
        private readonly List<Transaction> history = new List<Transaction>();

        public MemoryEventSource(int batchSize = 10)
        {
            this.batchSize = batchSize;
        }

        public IDisposable Subscribe(long? lastProcessedCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            lastCheckpoint = lastProcessedCheckpoint ?? 0;
            var subscriber = new Subscriber(lastCheckpoint, batchSize, handler);

            subscribers.Add(subscriber);

            Task.Run(async () =>
            {
                foreach (Transaction transaction in history)
                {
                    await subscriber.Send(new[] { transaction });
                }

            }).Wait();

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

            await Write(transaction);

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

                history.Add(transaction);
            }

            foreach (var subscriber in subscribers)
            {
                await subscriber.Send(transactions);
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

            await Write(transaction);

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
                    await handler(batch.ToList().AsReadOnly());
                }
            }
        }

        public void Dispose()
        {
            disposed = true;
        }
    }
}