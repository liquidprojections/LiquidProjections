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
        public class Given_a_raven_projector_with_an_inmemory_event_source : GivenWhenThen
        {
            protected readonly TaskCompletionSource<long> DispatchedCheckpointSource = new TaskCompletionSource<long>();
            protected RavenProjector<ProductCatalogEntry> Projector;
            protected LruProjectionCache<ProductCatalogEntry> Cache;

            public Given_a_raven_projector_with_an_inmemory_event_source()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    IDocumentStore store = new InMemoryRavenDbBuilder().Build();
                    UseThe(store);

                    Cache = new LruProjectionCache<ProductCatalogEntry>(1000, TimeSpan.Zero, TimeSpan.FromHours(1), () => DateTime.Now);

                    Projector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession, Cache);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await Projector.Handle(transactions);
                        DispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });
            }
        }

        public class When_an_event_is_mapped_as_an_update_and_no_projection_exists : Given_a_raven_projector_with_an_inmemory_event_source
        {
            private Transaction transaction;

            public When_an_event_is_mapped_as_an_update_and_no_projection_exists()
            {
                Given(() =>
                {
                    Projector.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey, (p, e, ctx) => p.Category = e.Category);
                });

                this.WhenAsync(async () =>
                {
                    transaction = await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
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
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
            }
        }

        public class When_an_event_is_mapped_as_a_delete : Given_a_raven_projector_with_an_inmemory_event_source
        {
            public When_an_event_is_mapped_as_a_delete()
            {
                this.GivenAsync(async () =>
                {
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
                        Category = "Hybrid"
                    });

                    Projector.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);
                });

                this.WhenAsync(async () =>
                {
                    The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    });

                    await DispatchedCheckpointSource.Task;
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
                Cache.CurrentCount.Should().Be(0);
            }
        }
        public class When_an_event_is_mapped_as_a_custom_action : Given_a_raven_projector_with_an_inmemory_event_source
        {
            public When_an_event_is_mapped_as_a_custom_action()
            {
                this.GivenAsync(async () =>
                {
                    using (var session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "c350E",
                            Category = "Hybrids"
                        });

                        await session.SaveChangesAsync();
                    }

                    Projector.Map<CategoryDiscontinuedEvent>().As(async (e, ctx) =>
                    {
                        var entries = await ctx.Session.Query<ProductCatalogEntry>()
                            .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                            .Where(en => en.Category == e.Category)
                            .ToListAsync();

                        foreach (var entry in entries)
                        {
                            ctx.Session.Delete(entry);
                        }
                    });
                });

                When(() =>
                {
                    The<MemoryEventSource>().Write(new CategoryDiscontinuedEvent
                    {
                        Category = "Hybrids",
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_have_executed_the_custom_action()
            {
                await DispatchedCheckpointSource.Task;

                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_is_mapped_as_an_update_and_the_projection_was_cached : Given_a_raven_projector_with_an_inmemory_event_source
        {
            public When_an_event_is_mapped_as_an_update_and_the_projection_was_cached()
            {
                Given(() =>
                {
                    Cache.Add(new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Hybrid"
                    });

                    Projector.Map<ProductAddedToCatalogEvent>().AsUpdateOf(e => e.ProductKey, (p, e, ctx) => p.Category = e.Category);
                });

                this.WhenAsync(async () =>
                {
                    The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid",
                        Version = 0
                    });

                    await DispatchedCheckpointSource.Task;
                });
            }

            [Fact]
            public void Then_it_should_get_it_from_the_cache()
            {
                Cache.Hits.Should().Be(1);
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

    public class CategoryDiscontinuedEvent
    {
        public string Category { get; set; }
    }
}