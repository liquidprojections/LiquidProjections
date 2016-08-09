using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class Dispatcher
    {
        private readonly IEventStore eventStore;

        public Dispatcher(IEventStore eventStore)
        {
            this.eventStore = eventStore;
        }

        public IDisposable Subscribe(long checkpoint, Func<IReadOnlyList<Transaction>, Task> handler)
        {
            return eventStore.Subscribe(checkpoint)
                .Select(transactions => Observable.FromAsync(() => handler(transactions)))
                .Concat()
                .Subscribe(_ => { }, _ =>
                {
                    /*
                       * Error occurred, avoid default behaviour that would just 
                       * throw and skip other subscribers from receiving it.
                       * */
                });
        }
    }
}