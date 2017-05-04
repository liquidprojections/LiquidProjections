using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    [Obsolete("Is replaced by the CreateSubscription delegate")]
    public interface IEventStore
    {
        IDisposable Subscribe(long? lastProcessedCheckpoint, Func<IReadOnlyList<Transaction>, Task> handler,
            string subscriptionId);
    }
}