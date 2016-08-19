using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections.NEventStore
{
    internal sealed class Subscriber
    {
        private readonly NEventStoreAdapter eventStoreClient;
        private readonly long? checkpoint;

        public Subscriber(NEventStoreAdapter eventStoreClient, long? checkpoint)
        {
            this.eventStoreClient = eventStoreClient;
            this.checkpoint = checkpoint;
        }

        public IDisposable Subscribe(Func<IReadOnlyList<Transaction>, Task> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            Subscription subscription;

            lock (eventStoreClient.subscriptionLock)
            {
                if (eventStoreClient.isDisposed)
                {
                    throw new ObjectDisposedException(typeof(NEventStoreAdapter).FullName);
                }

                subscription = new Subscription(eventStoreClient, checkpoint, observer);
                eventStoreClient.subscriptions.Add(subscription);
            }

            subscription.Start();
            return subscription;
        }
    }
}