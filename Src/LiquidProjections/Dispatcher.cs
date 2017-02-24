using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using LiquidProjections.Logging;

namespace LiquidProjections
{
    public class Dispatcher
    {
        private readonly IEventStoreWithSubscriptionIds eventStore;

        public Dispatcher(IEventStore eventStore)
            : this((eventStore as IEventStoreWithSubscriptionIds) ?? new EventStoreWithSubscriptionIdAdapter(eventStore))
        {
        }

        public Dispatcher(IEventStoreWithSubscriptionIds eventStore)
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            this.eventStore = eventStore;
        }

        public IDisposable Subscribe(long? checkpoint, Func<IReadOnlyList<Transaction>, Task> handler, string subscriptionId)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var subscriptionMonitor = new object();
            IDisposable subscription = null;

            lock (subscriptionMonitor)
            {
                subscription = eventStore.Subscribe(checkpoint,
                    async transactions =>
                    {
                        try
                        {
                            await handler(transactions);
                        }
                        catch (Exception exception)
                        {
                            LogProvider.GetCurrentClassLogger().FatalException(
                                "Projector exception was not handled. Event subscription has been cancelled.",
                                exception);

                            lock (subscriptionMonitor)
                            {
                                subscription.Dispose();
                            }
                        }
                    },
                    subscriptionId);
            }

            return subscription;
        }

        public IDisposable Subscribe(long? checkpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            return Subscribe(checkpoint, handler, null);
        }
    }
}