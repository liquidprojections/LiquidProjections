using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public interface IEventStore
    {
        IDisposable Subscribe(long? checkpoint, Func<IReadOnlyList<Transaction>, Task> handler);
    }
}