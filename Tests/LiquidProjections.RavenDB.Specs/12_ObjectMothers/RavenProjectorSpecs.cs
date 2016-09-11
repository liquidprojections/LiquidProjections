using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using LiquidProjections.RavenDB.Specs._05_TestDataBuilders;
using LiquidProjections.RavenDB.Specs._99_ObjectMothers.RavenProjectorSpecs;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs._12_ObjectMothers
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

            protected EventFactory A => new EventFactory(async @event =>
            {
                dispatchedCheckpointSource = new TaskCompletionSource<long>();

                await The<MemoryEventSource>().Write(@event);

                await dispatchedCheckpointSource.Task;
            });
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

                    await A.Product("c350e").WasAddedToCatalog("Hybrids");
                });

                When(async () =>
                {
                    await A.Product("c350e").WasDiscontinued();
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

namespace LiquidProjections.RavenDB.Specs._99_ObjectMothers.RavenProjectorSpecs
{
    public class EventFactory
    {
        private readonly Func<object, Task> writeEvent;

        public EventFactory(Func<object, Task> writeEvent)
        {
            this.writeEvent = writeEvent;
        }

        public ProductEventBuilder Product(string key)
        {
            return new ProductEventBuilder(key, writeEvent);
        }
    }

    public class ProductEventBuilder
    {
        private readonly string key;
        private readonly Func<object, Task> writeEvent;

        public ProductEventBuilder(string key, Func<object, Task> writeEvent)
        {
            this.key = key;
            this.writeEvent = writeEvent;
        }

        public Task WasAddedToCatalog(string category)
        {
            return writeEvent(new ProductAddedToCatalogEvent
            {
                ProductKey = key,
                Category = category
            });
        }

        public Task WasDiscontinued()
        {
            return writeEvent(new ProductDiscontinuedEvent
            {
                ProductKey = key,
            });
        }
    }
}