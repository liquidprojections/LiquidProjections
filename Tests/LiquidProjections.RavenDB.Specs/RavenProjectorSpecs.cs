using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs
{
    namespace RavenProjectorSpecs
    {
        public class When_an_event_is_mapped_as_an_update_and_no_projection_exists : GivenWhenThen
        {
            private Transaction transaction;

            private readonly TaskCompletionSource<long> dispatchedCheckpointSource = new TaskCompletionSource<long>();

            public When_an_event_is_mapped_as_an_update_and_no_projection_exists()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    IDocumentStore store = new InMemoryRavenDbBuilder().Build();
                    UseThe(store);

                    var ravenProjector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession);

                    ravenProjector.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey, (p, e, ctx) => p.Category = e.Category);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await ravenProjector.Handle(transactions);
                        dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });

                });

                When(() =>
                {
                    transaction = The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid",
                        Version = 0
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_update_the_projection()
            {
                long lastCheckpoint = await dispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
            }
        }
        public class When_an_event_is_mapped_as_a_delete : GivenWhenThen
        {
            private readonly TaskCompletionSource<bool> dispatchedSource = new TaskCompletionSource<bool>();
            private LruProjectionCache<ProductCatalogEntry> cache;

            public When_an_event_is_mapped_as_a_delete()
            {
                this.GivenAsync(async () =>
                {
                    UseThe(new MemoryEventSource());

                    IDocumentStore store = new InMemoryRavenDbBuilder().Build();
                    UseThe(store);

                    using (var session = store.OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "c350E"
                        });

                        await session.SaveChangesAsync();
                    }

                    cache = new LruProjectionCache<ProductCatalogEntry>(1000, TimeSpan.Zero, TimeSpan.FromHours(1), () => DateTime.Now);
                    cache.Add(new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Hybrid"
                    });

                    var ravenProjector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession, cache);
                    ravenProjector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await ravenProjector.Handle(transactions);
                        dispatchedSource.SetResult(true);
                    });

                });

                this.WhenAsync(async () =>
                {
                    The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    });

                    await dispatchedSource.Task;
                });
            }

            [Fact]
            public async Task Then_it_should_remove_the_projection()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }

            [Fact]
            public void Then_it_should_remove_it_from_the_cache_as_well()
            {
                cache.CurrentCount.Should().Be(0);
            }
        }

        public class When_an_event_is_mapped_as_an_update_and_the_projection_was_cached : GivenWhenThen
        {
            private readonly TaskCompletionSource<bool> dispatchedSource = new TaskCompletionSource<bool>();
            private LruProjectionCache<ProductCatalogEntry> cache;

            public When_an_event_is_mapped_as_an_update_and_the_projection_was_cached()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    IDocumentStore store = new InMemoryRavenDbBuilder().Build();
                    UseThe(store);

                    cache = new LruProjectionCache<ProductCatalogEntry>(1000, TimeSpan.Zero, TimeSpan.FromHours(1), () => DateTime.Now);
                    cache.Add(new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Hybrid"
                    });

                    var ravenProjector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession, cache);

                    ravenProjector.Map<ProductAddedToCatalogEvent>().AsUpdateOf(e => e.ProductKey, (p, e, ctx) => p.Category = e.Category);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await ravenProjector.Handle(transactions);
                        dispatchedSource.SetResult(true);
                    });
                });

                this.WhenAsync(async () =>
                {
                    The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid",
                        Version = 0
                    });

                    await dispatchedSource.Task;
                });
            }

            [Fact]
            public void Then_it_should_get_it_from_the_cache()
            {
                cache.Hits.Should().Be(1);
            }
            
            [Fact]
            public async Task But_it_should_still_update_the_raven_database()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
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
}