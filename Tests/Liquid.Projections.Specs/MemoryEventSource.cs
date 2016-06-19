using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eVision.QueryHost.Specs
{
    public class MemoryEventSource : IEventSource
    {
        private static long lastCheckpoint;

        private readonly List<Subscriber> subscribers = new List<Subscriber>();
        private readonly List<Transaction> transactions = new List<Transaction>();

        public Transaction Last
        {
            get { return transactions.LastOrDefault(); }
        }

        public IDisposable Subscribe(string fromCheckpoint, Func<Transaction, Task> onCommit)
        {
            var subscriber = new Subscriber(this)
            {
                Checkpoint = fromCheckpoint,
                OnCommit = onCommit
            };

            subscribers.Add(subscriber);

            foreach (var transaction in transactions)
            {
                subscriber.Notify(transaction).Wait();
            }

            return new CompositeDisposable();
        }

        public void RetrieveNow()
        {
        }

        public int CompareCheckpoints(string checkpointToken1, string checkpointToken2)
        {
            long checkpoint1 = string.IsNullOrWhiteSpace(checkpointToken1) ? 0 : int.Parse(checkpointToken1);
            long checkpoint2 = string.IsNullOrWhiteSpace(checkpointToken2) ? 0 : int.Parse(checkpointToken2);
            return checkpoint1.CompareTo(checkpoint2);
        }

        public async void Write(object @event)
        {
            await Write(new Transaction
            {
                Events =
                {
                    new Envelope
                    {
                        Body = @event
                    }
                }
            });
        }

        public async Task Write(Transaction transaction)
        {
            if (string.IsNullOrEmpty(transaction.Checkpoint))
            {
                transaction.Checkpoint = (++lastCheckpoint).ToString();
            }

            transactions.Add(transaction);

            foreach (Subscriber subscriber in subscribers)
            {
                await subscriber.Notify(transaction);
            }
        }

        private class Subscriber
        {
            private readonly IEventSource eventSource;

            public Subscriber(IEventSource eventSource)
            {
                this.eventSource = eventSource;
            }

            public string Checkpoint { get; set; }
            public Func<Transaction, Task> OnCommit { get; set; }

            public async Task Notify(Transaction transaction)
            {
                if (string.IsNullOrWhiteSpace(Checkpoint) || (eventSource.CompareCheckpoints(Checkpoint, transaction.Checkpoint) > 0))
                {
                    await OnCommit(transaction);
                }
            }
        }
    }
}