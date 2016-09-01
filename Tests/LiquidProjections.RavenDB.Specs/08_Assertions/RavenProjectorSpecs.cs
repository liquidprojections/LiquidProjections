using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using LiquidProjections.RavenDB.Specs._05_TestDataBuilders;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._08_Assertions
{
    namespace RavenProjectorSpecs
    {
        public class When_a_product_is_discontinued : GivenWhenThen
        {
            private TaskCompletionSource<long> dispatchedCheckpointSource;
            private LruProjectionCache<ProductCatalogEntry> cache;

            public When_a_product_is_discontinued()
            {
                Given(async () =>
                {
                    dispatchedCheckpointSource = new TaskCompletionSource<long>();

                    UseThe(new MemoryEventSource());

                    UseThe(await new RavenDbBuilder()
                        .AsInMemory
                        .Containing(new ProductCatalogEntryBuilder().IdentifiedBy("c350E").Build())
                        .Build());

                    cache = new LruProjectionCacheBuilder()
                        .Containing(new ProductCatalogEntryBuilder().IdentifiedBy("c350E").Build())
                        .Build<ProductCatalogEntry>();
                    
                    var projector = new RavenProjector<ProductCatalogEntry>(The<IDocumentStore>().OpenAsyncSession, cache);
                    projector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await projector.Handle(transactions);
                        dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    });

                    await dispatchedCheckpointSource.Task;
                });
            }

            [Fact]
            public async Task It_should_remove_the_catalog_entry_from_the_database()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull("because discontinued products should be removed from the database");
                }
            }

            [Fact]
            public void It_should_remove_the_catelog_entry_from_the_cache()
            {
                cache.CurrentCount.Should().Be(0, "because the discontinued product should be removed from the cache");
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