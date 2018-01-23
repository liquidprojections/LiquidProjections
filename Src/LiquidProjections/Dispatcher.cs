using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiquidProjections.Abstractions;
using LiquidProjections.Logging;

namespace LiquidProjections
{
    /// <summary>
    /// Serves as the entry point for subscribers and provides policies for handling exceptions thrown by subscribers.
    /// </summary>
    public class Dispatcher
    {
        private static readonly Task<ExceptionResolution> AbortExceptionResolutionTask = Task.FromResult(ExceptionResolution.Abort);

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
                HandleTransactions = async (transactions, info) => await HandleTransactions(transactions, handler, info).ConfigureAwait(false),
                NoSuchCheckpoint = async info => await HandleUnknownCheckpoint(info, handler, options).ConfigureAwait(false)
            }, options.Id);
        }

        /// <summary>
        /// Configures the behavior of the dispatcher in case an exception is thrown by a subscriber. The default
        /// behavior is to dispose of the subscription.
        /// </summary>
        public HandleException ExceptionHandler { get; set; } = (e, attempts, info) => AbortExceptionResolutionTask;

        public HandleSuccess SuccessHandler { get; set; } = _ => SpecializedTasks.FalseTask;

        private async Task HandleTransactions(IReadOnlyList<Transaction> transactions, Func<IReadOnlyList<Transaction>, SubscriptionInfo, Task> handler, SubscriptionInfo info)
        {
            await ExecuteWithPolicy(
                info,
                async () =>
                {
                    await handler(transactions, info).ConfigureAwait(false);
                    await SuccessHandler(info).ConfigureAwait(false);
                },
                abort: exception =>
                {
                    LogProvider.GetLogger(typeof(Dispatcher)).FatalException(
                        "Projector exception was not handled. Event subscription has been cancelled.",
                        exception);

                    info.Subscription?.Dispose();
                })
                .ConfigureAwait(false);
        }

        private async Task HandleUnknownCheckpoint(SubscriptionInfo info, Func<IReadOnlyList<Transaction>, SubscriptionInfo, Task> handler, SubscriptionOptions options)
        {
            if (options.RestartWhenAhead)
            {
                await ExecuteWithPolicy(
                    info,
                    async () =>
                    {
                        await options.BeforeRestarting().ConfigureAwait(false);
    
                        Subscribe(null, handler, options);
                    },
                    abort: exception =>
                    {
                        LogProvider.GetLogger(typeof(Dispatcher)).FatalException(
                            "Failed to restart the projector.",
                            exception);
                    },
                    ignore: () => Subscribe(null, handler, options))
                    .ConfigureAwait(false);

                // Dispose the subscription only after a new subscription is created
                // to support CreateSubscription delegates which wait
                // until the subscription detects being ahead
                // and until all the existing transactions are processed by the new subscription
                info.Subscription?.Dispose();
            }
        }

        private async Task ExecuteWithPolicy(SubscriptionInfo info, Func<Task> action, Action<Exception> abort, Action ignore = null)
        {
            int attempts = 0;
            bool retry = true;
            do
            {
                try
                {
                    attempts++;
                    await action().ConfigureAwait(false);
                    retry = false;
                }
                catch (Exception exception)
                {
                    ExceptionResolution resolution = await ExceptionHandler(exception, attempts, info).ConfigureAwait(false);
                    switch (resolution)
                    {
                        case ExceptionResolution.Abort:
                            abort(exception);
                            retry = false;
                            break;

                        case ExceptionResolution.Retry:
                            break;

                        case ExceptionResolution.Ignore:
                            retry = false;
                            ignore?.Invoke();
                            break;
                    }
                }
            }
            while (retry);
        }
    }

    /// <summary>
    /// Defines the signature for a method that handles an exception while dispatching transactions.
    /// </summary>
    /// <param name="exception">The exception that was caught by the <see cref="Dispatcher"/></param>
    /// <param name="attempts">Counts the number times the action involved was attempted. Starts at <c>1</c></param>
    /// <param name="info">Information about the subscription.</param>
    /// <returns>
    /// Instructs the <see cref="Dispatcher"/> on how to resolve the exception.
    /// </returns>
    public delegate Task<ExceptionResolution> HandleException(Exception exception, int attempts, SubscriptionInfo info);

    /// <summary>
    /// Defines the behavior in case a subscriber throws an exception.
    /// </summary>
    public enum ExceptionResolution
    {
        Ignore,
        Abort,
        Retry
    }

    /// <summary>
    /// Defines the signature for a method that handles a successful transaction dispatching iteration.
    /// </summary>
    /// <param name="info">Information about the subscription.</param>
    public delegate Task HandleSuccess(SubscriptionInfo info);

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
        public Func<Task> BeforeRestarting { get; set; } = () => SpecializedTasks.ZeroTask;
    }
}