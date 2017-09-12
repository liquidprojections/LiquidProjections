using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections.Abstractions
{
    /// <summary>
    /// Creates a subscription on an event store, starting at the transaction following 
    /// <paramref name="lastProcessedCheckpoint"/>, identified by <paramref name="subscriptionId"/>, and which 
    /// passes any transactions to the provided <paramref name="subscriber"/>.
    /// </summary>
    /// <param name="lastProcessedCheckpoint"> 
    /// The checkpoint of the transaction the subscriber has last seen, or <c>null</c> to start from the beginning.
    /// </param>
    /// <param name="subscriber">
    /// An object wrapping the various handlers that the subscription will use.
    /// </param>
    /// <param name="subscriptionId">
    /// Identifies this subscription and helps distinct multiple subscriptions. 
    /// </param>
    /// <returns>
    /// A disposable object that will cancel the subscription.
    /// </returns>
    public delegate IDisposable CreateSubscription(long? lastProcessedCheckpoint,
        Subscriber subscriber, string subscriptionId);

    public class Subscriber
    {
        /// <summary>
        /// Represents a handler that will receive the transactions that the event store pushes to the subscriber.
        /// </summary>
        public Func<IReadOnlyList<Transaction>, SubscriptionInfo, Task> HandleTransactions { get; set; }

        /// <summary>
        /// Represents a handler that the event store will use if the requested checkpoint does not exist. 
        /// </summary>
        public Func<SubscriptionInfo, Task> NoSuchCheckpoint { get; set; }
    }
}