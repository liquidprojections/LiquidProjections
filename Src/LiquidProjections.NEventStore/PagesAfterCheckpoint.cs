using System;
using System.Collections.Generic;

namespace LiquidProjections.NEventStore
{
    internal sealed class PagesAfterCheckpoint : IObservable<IReadOnlyList<Transaction>>
    {
        private readonly NEventStoreAdapter eventStoreClient;
        private readonly long? checkpoint;

        public PagesAfterCheckpoint(NEventStoreAdapter eventStoreClient, long? checkpoint)
        {
            this.eventStoreClient = eventStoreClient;
            this.checkpoint = checkpoint;
        }

        public IDisposable Subscribe(IObserver<IReadOnlyList<Transaction>> observer)
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