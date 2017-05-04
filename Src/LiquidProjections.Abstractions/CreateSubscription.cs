using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections.Abstractions
{
    /// <summary>
    /// Creates a subscription on an event store, starting at the transaction following 
    /// <paramref name="lastProcessedCheckpoint"/>, identified by <paramref name="subscriptionId"/>, and which 
    /// passed transactions to the provided <paramref name="handler"/>.
    /// </summary>
    /// <param name="lastProcessedCheckpoint"></param>
    /// <param name="handler"></param>
    /// <param name="subscriptionId"></param>
    /// <returns></returns>
    public delegate IDisposable CreateSubscription(long? lastProcessedCheckpoint,
        Func<IReadOnlyList<Transaction>, Task> handler,
        string subscriptionId);
}