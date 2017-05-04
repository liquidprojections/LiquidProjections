using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiquidProjections.Abstractions;
using LiquidProjections.Logging;

namespace LiquidProjections
{
    public class Dispatcher
    {
        private readonly CreateSubscription createSubscription;

        [Obsolete("The IEventStore interface has been replaced by the SubscribeToEvents delegate")]
        public Dispatcher(IEventStore eventStore)
        {
            if (eventStore == null)
            {
                throw new ArgumentNullException(nameof(eventStore));
            }

            this.createSubscription = eventStore.Subscribe;
        }

        public Dispatcher(CreateSubscription createSubscription)
        {
            this.createSubscription = createSubscription;
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
                subscription = createSubscription(checkpoint,
                    async transactions =>
                    {
                        try
                        {
                            await handler(transactions);
                        }
                        catch (Exception exception)
                        {
                            LogProvider.GetLogger(typeof(Dispatcher)).FatalException(
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