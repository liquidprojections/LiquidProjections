using System;
using System.Collections.Generic;
using System.Linq;

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

        public IObservable<IReadOnlyList<Transaction>> Subscribe(long? fromCheckpoint)
        {
            var subscriber = new Subscriber(fromCheckpoint ?? 0, history, batchSize);

            subscribers.Add(subscriber);

            return subscriber;
        }


        public Transaction Write(object @event)
        {
            Transaction transaction = new Transaction
            {
                Events =
                {
                    new EventEnvelope
                    {
                        Body = @event
                    }
                }
            };

            Write(transaction);

            return transaction;
        }

        public void Write(params Transaction[] transactions)
        {
            foreach (var transaction in transactions)
            {
                transaction.Checkpoint = (++lastCheckpoint);
                history.Add(transaction);
            }

            foreach (var subscriber in subscribers)
            {
                subscriber.OnNext(transactions);
            }
        }
    }

    internal class Subscriber : IObservable<IReadOnlyList<Transaction>>, IDisposable
    {
        private readonly long fromCheckpoint;
        private readonly int batchSize;
        private IObserver<IReadOnlyList<Transaction>> observer;
        private readonly IEnumerable<Transaction> priorTransactions;

        public Subscriber(long fromCheckpoint, IEnumerable<Transaction> transactions, int batchSize)
        {
            this.fromCheckpoint = fromCheckpoint;
            this.batchSize = batchSize;
            priorTransactions = transactions;
        }

        public bool Disposed { get; private set; } = false;

        public IDisposable Subscribe(IObserver<IReadOnlyList<Transaction>> observer)
        {
            this.observer = observer;
            OnNext(priorTransactions);

            return this;
        }

        public void OnNext(IEnumerable<Transaction> transactions)
        {
            if (!Disposed)
            {
                foreach (var batch in transactions.Where(t => t.Checkpoint >= fromCheckpoint).InBatchesOf(batchSize))
                {
                    observer.OnNext(batch.ToList().AsReadOnly());
                }
            }
        }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}