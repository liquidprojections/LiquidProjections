using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.PlatformServices;
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
            // Rx-workaround (it has hardcoded assembly reference which prevents ILMerging the assembly)
            EnlightenmentProvider.EnsureLoaded();

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