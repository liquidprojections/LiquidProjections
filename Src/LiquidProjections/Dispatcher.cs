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
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            this.eventStore = eventStore;
        }

        public void Subscribe(long? checkpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // TODO: intercept and log errors

            eventStore.Subscribe(checkpoint, async transactions =>
            {
                try
                {
                    await handler(transactions);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });
        }
    }
}