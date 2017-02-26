using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections.NEventStore
{
    internal sealed class Subscriber
    {
        private readonly NEventStoreAdapter eventStoreClient;
        private readonly long previousCheckpoint;

        public Subscriber(NEventStoreAdapter eventStoreClient, long previousCheckpoint, string subscriptionId)
        {
            this.eventStoreClient = eventStoreClient;
            this.previousCheckpoint = previousCheckpoint;
            SubscriptionId = subscriptionId;
        }

        public string SubscriptionId { get; }

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

                subscription = new Subscription(eventStoreClient, previousCheckpoint, observer, SubscriptionId);
                eventStoreClient.subscriptions.Add(subscription);
            }

            subscription.Start();
            return subscription;
        }
    }
}