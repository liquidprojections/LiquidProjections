using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class Dispatcher
    {
        private readonly IEventStore eventStore;

        public Dispatcher(IEventStore eventStore)
        {
            this.eventStore = eventStore;
        }

        public void Subscribe(long checkpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            // TODO: intercept and log errors

            eventStore.Subscribe(checkpoint, handler);
        }
    }
}