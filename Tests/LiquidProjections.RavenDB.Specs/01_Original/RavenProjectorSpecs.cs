using System;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client;
using Raven.Client.Embedded;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._01_Original
{
    public class RavenProjectorSpecs
    {
        [Fact]
        public async Task
            When_the_RavenProjector_projects_a_ProductDiscontinuedEvent_from_the_in_memory_event_source_it_should_remove_the_ProductCatalogEntry_from_RavenDB
            ()
        {
            var dispatchedCheckpointSource = new TaskCompletionSource<long>();

            var eventSource = new MemoryEventSource();

            IDocumentStore store = new EmbeddableDocumentStore
            {
                RunInMemory = true,
                Configuration = { RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true }
            }.Initialize();

            var cache = new LruProjectionCache<ProductCatalogEntry>(1000, TimeSpan.Zero, TimeSpan.FromHours(1),
                () => DateTime.Now);

            string productKey = "c350E";
            string category = "Hybrid";

            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(new ProductCatalogEntry
                {
                    Id = productKey
                });

                await session.SaveChangesAsync();
            }

            cache.Add(new ProductCatalogEntry
            {
                Id = productKey,
                Category = category
            });

            var projector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession, cache);
            projector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

            var dispatcher = new Dispatcher(eventSource);
            dispatcher.Subscribe(0, async transactions =>
            {
                await projector.Handle(transactions);
                dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
            });

            await eventSource.Write(new ProductDiscontinuedEvent
            {
                ProductKey = productKey,
            });

            await dispatchedCheckpointSource.Task;

            using (var session = store.OpenAsyncSession())
            {
                var entry = await session.LoadAsync<ProductCatalogEntry>(productKey);
                Assert.Null(entry);
            }

            Assert.Equal(cache.CurrentCount, 0);
        }
    }

    public class ProductCatalogEntry : IHaveIdentity
    {
        public string Id { get; set; }
        public string Category { get; set; }
    }

    public class ProductAddedToCatalogEvent
    {
        public string ProductKey { get; set; }
        public string Category { get; set; }

        public long Version { get; set; }
    }

    public class ProductDiscontinuedEvent
    {
        public string ProductKey { get; set; }
    }

    public class CategoryDiscontinuedEvent
    {
        public string Category { get; set; }
    }
}