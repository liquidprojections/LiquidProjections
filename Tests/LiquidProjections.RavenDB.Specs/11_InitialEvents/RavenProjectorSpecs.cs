using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using LiquidProjections.RavenDB.Specs._05_TestDataBuilders;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._11_InitialEvents
{
    namespace RavenProjectorSpecs
    {
        public class Given_a_raven_projector_with_an_in_memory_event_source : GivenWhenThen
        {
            private TaskCompletionSource<long> dispatchedCheckpointSource;
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
                        dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });

            }
            protected async Task Write(object @event)
            {
                dispatchedCheckpointSource = new TaskCompletionSource<long>();

                await The<MemoryEventSource>().Write(@event);

                await dispatchedCheckpointSource.Task;
            }
        }

        public class When_a_product_is_discontinued : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_a_product_is_discontinued()
            {
                Given(async () =>
                {
                    Projector.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey, (p, e, ctx) => p.Category = e.Category);

                    Projector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    await Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                    });
                });

                When(async () =>
                {
                    await Write(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    });
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