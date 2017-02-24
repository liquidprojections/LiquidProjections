using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    internal class EventStoreWithSubscriptionIdAdapter : IEventStoreWithSubscriptionIds
    {
        private readonly IEventStore eventStore;

        public EventStoreWithSubscriptionIdAdapter(IEventStore eventStore)
        {
            this.eventStore = eventStore;
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }
        }

        public IDisposable Subscribe(long? lastProcessedCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            return eventStore.Subscribe(lastProcessedCheckpoint, handler);
        }

        public IDisposable Subscribe(long? lastProcessedCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler,
            string subscriptionId)
        {
            return eventStore.Subscribe(lastProcessedCheckpoint, handler);
        }
    }
}