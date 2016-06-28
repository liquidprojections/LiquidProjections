using System;
using System.Collections.Generic;

namespace LiquidProjections
{
    public interface IEventStore
    {
        IObservable<IReadOnlyList<Transaction>> Subscribe(string checkpoint);
    }
}