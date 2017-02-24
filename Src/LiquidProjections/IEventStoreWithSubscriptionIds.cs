using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public interface IEventStoreWithSubscriptionIds : IEventStore
    {
        IDisposable Subscribe(long? lastProcessedCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler,
            string subscriptionId);
    }
}