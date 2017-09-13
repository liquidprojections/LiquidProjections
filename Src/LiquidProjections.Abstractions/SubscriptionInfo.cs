using System;
using System.Threading;

namespace LiquidProjections.Abstractions
{
    public class SubscriptionInfo
    {
        public string Id { get; set; }

        public IDisposable Subscription { get; set; }
        
        /// <summary>
        /// The cancellation is requested when the subscription is being cancelled.
        /// The cancellation token is disposed and cannot be used after the subscription cancellation is completed.
        /// Old versions of event stores do not have the cancellation token. 
        /// </summary>
        public CancellationToken? CancellationToken { get; set; }
    }
}