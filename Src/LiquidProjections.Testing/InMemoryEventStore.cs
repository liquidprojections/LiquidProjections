using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using LiquidProjections.Testing;

namespace LiquidProjections
{
    public class MemoryEventSource : IEventStore
    {
        private readonly int batchSize;
        private static long lastCheckpoint;

        private readonly List<Subscriber> subscribers = new List<Subscriber>();
        private readonly List<Transaction> history = new List<Transaction>();

        public MemoryEventSource(int batchSize = 10)
        {
            this.batchSize = batchSize;
        }

        public IDisposable Subscribe(long? fromCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            var subscriber = new Subscriber(fromCheckpoint ?? 0, batchSize, handler);

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
        private readonly long fromCheckpoint;
        private readonly int batchSize;
        private readonly Func<IReadOnlyList<Transaction>, Task> handler;
        
        public Subscriber(long fromCheckpoint, int batchSize, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            this.fromCheckpoint = fromCheckpoint;
            this.batchSize = batchSize;
            this.handler = handler;
        }

        public bool Disposed { get; private set; } = false;

        public async Task Send(IEnumerable<Transaction> transactions)
        {
            if (!Disposed)
            {
                foreach (var batch in transactions.Where(t => t.Checkpoint >= fromCheckpoint).InBatchesOf(batchSize))
                {
                    await handler(batch.ToList().AsReadOnly()).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}