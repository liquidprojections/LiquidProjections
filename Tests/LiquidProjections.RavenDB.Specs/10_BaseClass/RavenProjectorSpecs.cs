using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using LiquidProjections.RavenDB.Specs._05_TestDataBuilders;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._10_BaseClass
{
    namespace RavenProjectorSpecs
    {
        public class Given_a_raven_projector_with_an_in_memory_event_source : GivenWhenThen
        {
            protected readonly TaskCompletionSource<long> DispatchedCheckpointSource = new TaskCompletionSource<long>();
            protected RavenProjector<ProductCatalogEntry> Projector;
            protected LruProjectionCache<ProductCatalogEntry> Cache;

            public Given_a_raven_projector_with_an_in_memory_event_source()
            {
                Given(async () =>
                {
                    UseThe(new MemoryEventSource());

                    UseThe(await new RavenDbBuilder().AsInMemory.Build());

                    Cache = new LruProjectionCacheBuilder().Build<ProductCatalogEntry>();

                    Projector = new RavenProjector<ProductCatalogEntry>(The<IDocumentStore>().OpenAsyncSession, Cache);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await Projector.Handle(transactions);
                        DispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });
            }
        }
        
        public class When_a_product_is_discontinued : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_a_product_is_discontinued()
            {
                Given(async () =>
                {
                    Projector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);
                    
                    using (var session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "c350E"
                        });

                        await session.SaveChangesAsync();
                    }

                    Cache.Add(new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Hybrids"
                    });
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    });

                    await DispatchedCheckpointSource.Task;
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
            public void It_should_remove_the_catalog_entry_from_the_cache()
            {
                Cache.CurrentCount.Should().Be(0, "because the discontinued product should be removed from the cache");
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