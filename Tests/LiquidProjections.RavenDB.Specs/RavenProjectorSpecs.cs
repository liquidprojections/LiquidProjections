using System;
using System.Linq;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using LiquidProjections.RavenDB.Specs.RavenProjectorSpecs;
using Raven.Client;
using Xunit;

namespace LiquidProjections.RavenDB.Specs
{
    namespace RavenProjectorSpecs
    {
        public class Given_a_raven_projector_with_an_in_memory_event_source : GivenWhenThen
        {
            protected readonly TaskCompletionSource<long> DispatchedCheckpointSource = new TaskCompletionSource<long>();
            protected RavenProjector<ProductCatalogEntry> Projector;
            protected LruProjectionCache<ProductCatalogEntry> Cache;
            protected EventMap<ProductCatalogEntry, RavenProjectionContext> Events;

            public Given_a_raven_projector_with_an_in_memory_event_source()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    IDocumentStore store = new InMemoryRavenDbBuilder().Build();
                    UseThe(store);

                    Cache = new LruProjectionCache<ProductCatalogEntry>(1000, TimeSpan.Zero, TimeSpan.FromHours(1), () => DateTime.Now);

                    Events = new EventMap<ProductCatalogEntry, RavenProjectionContext>();

                    Projector = new RavenProjector<ProductCatalogEntry>(store.OpenAsyncSession, Events, 10, Cache);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());
                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await Projector.Handle(transactions);
                        DispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });
            }
        }

        public class When_an_event_requires_an_update_of_a_non_existing_projection : Given_a_raven_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_an_update_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);
                });

                When(async () =>
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
            public async Task Then_it_should_create_a_new_projection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
            }
            
            [Fact]
            public async Task Then_it_should_track_the_transaction_checkpoint()
            {
                var checkpoint = await Projector.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_unloaded_projection_must_be_deleted : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_unloaded_projection_must_be_deleted()
            {
                Given(async () =>
                {
                    using (var session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "ProductCatalogEntry/c350E"
                        });

                        await session.SaveChangesAsync();
                    }

                    Cache.Add(new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Hybrid"
                    });

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);
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
            public async Task Then_it_should_remove_the_projection()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().BeNull();
                }
            }

            [Fact]
            public void Then_it_should_remove_it_from_the_cache_as_well()
            {
                Cache.CurrentCount.Should().Be(0);
            }
        }
        
        public class When_an_event_deletes_an_unloaded_projection : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_deletes_an_unloaded_projection()
            {
                Given(() =>
                {
                    Cache.Add(new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Hybrid"
                    });

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);
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
            public async Task Then_it_should_remove_the_projection()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().BeNull();
                }
            }

            [Fact]
            public void Then_it_should_remove_it_from_the_cache_as_well()
            {
                Cache.CurrentCount.Should().Be(0);
            }
        }

        public class When_an_event_requires_as_a_custom_action : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_as_a_custom_action()
            {
                Given(async () =>
                {
                    using (var session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "ProductCatalogEntry/c350E",
                            Category = "Hybrids"
                        });

                        await session.SaveChangesAsync();
                    }

                    Events.Map<CategoryDiscontinuedEvent>().As(async (e, ctx) =>
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

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new CategoryDiscontinuedEvent
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
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_a_modified_projection_must_be_deleted : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_a_modified_projection_must_be_deleted()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(
                        new ProductAddedToCatalogEvent
                        {
                            ProductKey = "c350E",
                            Category = "Hybrids"
                        }, new ProductDiscontinuedEvent
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
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_an_update_of_a_cached_projection : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_of_a_cached_projection()
            {
                Given(() =>
                {
                    Cache.Add(new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Hybrid"
                    });

                    Events.Map<ProductAddedToCatalogEvent>().AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
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
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
            }
        }

        public class When_an_event_is_not_mapped_at_all : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_is_not_mapped_at_all()
            {
                Given(() =>
                {
                    Cache.Add(new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Hybrid"
                    });
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid",
                        Version = 0
                    });

                    await DispatchedCheckpointSource.Task;
                });
            }

            [Fact]
            public void Then_it_should_not_do_anything()
            {
                Cache.Hits.Should().Be(0);
            }
        }

        public class When_a_custom_collection_name_is_set : Given_a_raven_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_a_custom_collection_name_is_set()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    Projector.CollectionName = "CatalogEntries";
                });

                When(async () =>
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
            public async Task Then_it_should_create_the_projection_under_that_collection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("CatalogEntries/c350E");
                    entry.Should().NotBeNull();
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