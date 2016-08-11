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
                    // TODO: Properly handle errors
                });
        }
    }
}