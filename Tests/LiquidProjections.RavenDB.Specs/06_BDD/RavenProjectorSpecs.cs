using System.Linq;
using System.Threading.Tasks;
using LiquidProjections.RavenDB.Specs._05_TestDataBuilders;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._06_BDD
{
    namespace RavenProjectorSpecs
    {
        public class When_a_product_is_discontinued : SpecificationContext
        {
            private TaskCompletionSource<long> dispatchedCheckpointSource;
            private MemoryEventSource eventSource;
            private IDocumentStore store;
            private LruProjectionCache<ProductCatalogEntry> cache;

            protected override async Task EstablishContext()
            {
                dispatchedCheckpointSource = new TaskCompletionSource<long>();

                eventSource = new MemoryEventSource();

                store = await new RavenDbBuilder()
                    .AsInMemory
                    .Containing(new ProductCatalogEntryBuilder().IdentifiedBy("c350E").Build())
                    .Build();

                cache = new LruProjectionCacheBuilder()
                    .Containing(new ProductCatalogEntryBuilder().IdentifiedBy("c350E").Build())
                    .Build<ProductCatalogEntry>();
               
                var projector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession, cache);
                projector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                var dispatcher = new Dispatcher(eventSource);
                dispatcher.Subscribe(0, async transactions =>
                {
                    await projector.Handle(transactions);
                    dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                });
            }

            protected override async Task Because()
            {
                await eventSource.Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                });
            }

            [Fact]
            public async Task It_should_remove_the_catalog_entry_from_the_database()
            {
                await dispatchedCheckpointSource.Task;

                using (var session = store.OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    Assert.Null(entry);
                }
            }

            [Fact]
            public void It_should_remove_the_catelog_entry_from_the_cache()
            {
                Assert.Equal(cache.CurrentCount, 0);
            }
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