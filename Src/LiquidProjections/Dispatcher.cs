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

        public Dispatcher(CreateSubscription createSubscription)
        {
            this.createSubscription = createSubscription;
        }

        public IDisposable Subscribe(long? lastProcessedCheckpoint,
            Func<IReadOnlyList<Transaction>, SubscriptionInfo, Task> handler,
            SubscriptionOptions options = null)
        {
            if (options == null)
            {
                options = new SubscriptionOptions();
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            return createSubscription(lastProcessedCheckpoint, new Subscriber
            {
                HandleTransactions = async (transactions, info) => await HandleTransactions(transactions, handler, info),
                NoSuchCheckpoint = async info => await HandleUnknownCheckpoint(info, handler, options)
            }, options.Id);
        }

        private static async Task HandleTransactions(IReadOnlyList<Transaction> transactions, Func<IReadOnlyList<Transaction>, SubscriptionInfo, Task> handler, SubscriptionInfo info)
        {
            try
            {
                await handler(transactions, info);
            }
            catch (Exception exception)
            {
                LogProvider.GetLogger(typeof(Dispatcher)).FatalException(
                    "Projector exception was not handled. Event subscription has been cancelled.",
                    exception);

                info.Subscription?.Dispose();
            }
        }

        private async Task HandleUnknownCheckpoint(SubscriptionInfo info, Func<IReadOnlyList<Transaction>, SubscriptionInfo, Task> handler, SubscriptionOptions options)
        {
            if (options.RestartWhenAhead)
            {
                try
                {
                    info.Subscription?.Dispose();

                    await options.BeforeRestarting();

                    Subscribe(null, handler, options);
                }
                catch (Exception exception)
                {
                    LogProvider.GetLogger(typeof(Dispatcher)).FatalException(
                        "Failed to restart the projector.",
                        exception);
                }
            }
        }
    }

    public class SubscriptionOptions
    {
        /// <summary>
        /// Can be used by subscribers to understand which is which.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// If set to <c>true</c>, the dispatcher will automatically restart at the first transaction if it detects
        /// that the subscriber is ahead of the event store (e.g. because it got restored to an earlier time).
        /// </summary>
        public bool RestartWhenAhead { get; set; }

        /// <summary>
        /// If restarting is enabled through <see cref="RestartWhenAhead"/>, this property can be used to run some
        /// clean-up code before the dispatcher will restart at the first transaction.
        /// </summary>
        public Func<Task> BeforeRestarting { get; set; } = () => Task.FromResult(0);
    }
}