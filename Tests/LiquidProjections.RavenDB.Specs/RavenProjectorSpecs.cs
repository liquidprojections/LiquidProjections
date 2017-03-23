using System;
using System.Collections.Generic;
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
        public class Given_a_raven_projector_with_an_in_memory_event_source : GivenSubject<RavenProjector<ProductCatalogEntry>>
        {
            protected EventMapBuilder<ProductCatalogEntry, string, RavenProjectionContext> Events;

            public Given_a_raven_projector_with_an_in_memory_event_source()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());
                    UseThe(new InMemoryRavenDbBuilder().Build());
                    UseThe(new LruProjectionCache(1000, TimeSpan.Zero, TimeSpan.FromHours(1), () => DateTime.UtcNow));
                    Events = new EventMapBuilder<ProductCatalogEntry, string, RavenProjectionContext>();
                });
            }

            protected void StartProjecting(string collectionName = null, IRavenChildProjector[] children = null)
            {
                WithSubject(_ => new RavenProjector<ProductCatalogEntry>(
                    The<IDocumentStore>().OpenAsyncSession, Events, children)
                {
                    BatchSize = 10,
                    Cache = The<LruProjectionCache>()
                });

                if (!string.IsNullOrEmpty(collectionName))
                {
                    Subject.CollectionName = collectionName;
                }

                The<MemoryEventSource>().Subscribe(0, Subject.Handle, "");
            }
        }

        public class When_an_event_requires_a_create_of_a_new_projection : Given_a_raven_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_of_a_new_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    StartProjecting();
                });

                When(async () =>
                {
                    transaction = await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_create_the_projection()
            {
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
                var checkpoint = await Subject.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_create_of_an_existing_cached_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_create_of_an_existing_cached_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    The<LruProjectionCache>().Add(existingEntry);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }), deferedExecution: true);
            }

            [Fact]
            public void Then_it_should_throw()
            {
                WhenAction.ShouldThrow<ProjectionException>();
            }
        }

        public class When_an_event_requires_a_create_of_an_existing_unloaded_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_create_of_an_existing_unloaded_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }), deferedExecution: true);
            }

            [Fact]
            public void Then_it_should_throw()
            {
                WhenAction.ShouldThrow<ProjectionException>();
            }
        }

        public class When_an_event_requires_a_create_if_does_not_exist_of_a_new_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_if_does_not_exist_of_a_new_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateIfDoesNotExistOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    StartProjecting();
                });

                When(async () =>
                {
                    transaction = await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_create_the_projection()
            {
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
                var checkpoint = await Subject.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_create_if_does_not_exist_of_an_existing_cached_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_create_if_does_not_exist_of_an_existing_cached_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateIfDoesNotExistOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    The<LruProjectionCache>().Add(existingEntry);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_do_nothing()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Gas");
                }
            }
        }

        public class When_an_event_requires_a_create_if_does_not_exists_of_an_existing_unloaded_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_create_if_does_not_exists_of_an_existing_unloaded_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateIfDoesNotExistOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_do_nothing()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Gas");
                }
            }
        }

        public class When_an_event_requires_a_create_or_update_of_a_new_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_or_update_of_a_new_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOrUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    StartProjecting();
                });

                When(async () =>
                {
                    transaction = await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_create_the_projection()
            {
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
                var checkpoint = await Subject.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_create_or_update_of_an_existing_cached_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_create_or_update_of_an_existing_cached_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOrUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    The<LruProjectionCache>().Add(existingEntry);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_update_the_projection()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }
        }

        public class When_an_event_requires_a_create_or_update_of_an_existing_unloaded_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_create_or_update_of_an_existing_unloaded_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOrUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_update_the_projection()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }
        }

        public class When_an_event_requires_a_delete_of_non_existing_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }), deferedExecution: true);
            }

            [Fact]
            public void Then_it_should_throw()
            {
                WhenAction.ShouldThrow<ProjectionException>();
            }
        }

        public class When_an_event_requires_a_delete_of_an_unloaded_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_an_unloaded_projection()
            {
                Given(async () =>
                {
                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "ProductCatalogEntry/c350E",
                            Category = "Hybrid"
                        });

                        await session.SaveChangesAsync();
                    }

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }));
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
                The<LruProjectionCache>().CurrentCount.Should().Be(0);
            }
        }

        public class When_an_event_requires_a_delete_of_a_cached_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_a_cached_projection()
            {
                Given(async () =>
                {
                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Hybrid"
                    };

                    The<LruProjectionCache>().Add(existingEntry);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(async () => await The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }));
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
                The<LruProjectionCache>().CurrentCount.Should().Be(0);
            }
        }

        public class When_an_event_requires_a_delete_of_a_modified_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_a_modified_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "ProductCatalogEntry/c350E",
                            Category = "Gas"
                        });

                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(
                    new ProductMovedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrids"
                    },
                    new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    }));
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

        public class When_an_event_requires_a_delete_of_an_added_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_an_added_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(
                    new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrids"
                    },
                    new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    }));
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

        public class When_an_event_requires_a_delete_if_exists_of_non_existing_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(async () => await The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }), deferedExecution: true);
            }

            [Fact]
            public void Then_it_should_do_nothing()
            {
                WhenAction.ShouldNotThrow();
            }
        }

        public class When_an_event_requires_a_delete_if_exists_of_an_unloaded_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_an_unloaded_projection()
            {
                Given(async () =>
                {
                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "ProductCatalogEntry/c350E",
                            Category = "Hybrid"
                        });

                        await session.SaveChangesAsync();
                    }

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }));
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
                The<LruProjectionCache>().CurrentCount.Should().Be(0);
            }
        }

        public class When_an_event_requires_a_delete_if_exists_of_a_cached_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_a_cached_projection()
            {
                Given(async () =>
                {
                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Hybrid"
                    };

                    The<LruProjectionCache>().Add(existingEntry);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }));
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
                The<LruProjectionCache>().CurrentCount.Should().Be(0);
            }
        }

        public class When_an_event_requires_a_delete_if_exists_of_a_modified_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_a_modified_projection()
            {
                Given(async () =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(e => e.ProductKey);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(new ProductCatalogEntry
                        {
                            Id = "ProductCatalogEntry/c350E",
                            Category = "Gas"
                        });

                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(
                    new ProductMovedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrids"
                    },
                    new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    }));
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

        public class When_an_event_requires_a_delete_if_exists_of_an_added_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_an_added_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey).Using((p, e, ctx) => p.Category = e.Category);

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(e => e.ProductKey);

                    StartProjecting();
                });

                When(async () => await The<MemoryEventSource>().Write(
                    new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrids"
                    },
                    new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    }));
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

        public class When_an_event_requires_a_custom_action : Given_a_raven_projector_with_an_in_memory_event_source
        {
            private RavenProjectionContext context;

            public When_an_event_requires_a_custom_action()
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

                        context = ctx;
                    });

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(
                    new Transaction
                    {
                        Checkpoint = 111,
                        Id = "MyTransactionId",
                        StreamId = "MyStreamId",
                        TimeStampUtc = 10.April(1979).At(13, 14, 15),
                        Headers = new Dictionary<string, object>
                        {
                            ["My custom header"] = "My custom header value"
                        },
                        Events = new List<EventEnvelope>
                        {
                            new EventEnvelope
                            {
                                Body = new CategoryDiscontinuedEvent
                                {
                                    Category = "Hybrids",
                                },
                                Headers = new Dictionary<string, object>
                                {
                                    ["Some event header"] = "Some event header value"
                                }
                            }
                        }
                    })
                );
            }

            [Fact]
            public async Task Then_it_should_have_executed_the_custom_action()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().BeNull();
                }
            }

            [Fact]
            public void Then_it_should_have_created_the_context()
            {
                context.ShouldBeEquivalentTo(new RavenProjectionContext
                {
                    Checkpoint = 111,
                    TransactionId = "MyTransactionId",
                    StreamId = "MyStreamId",
                    TimeStampUtc = 10.April(1979).At(13, 14, 15),
                    TransactionHeaders = new Dictionary<string, object>
                    {
                        ["My custom header"] = "My custom header value"
                    },
                    EventHeaders = new Dictionary<string, object>
                    {
                        ["Some event header"] = "Some event header value"
                    }
                }, options => options.Excluding(c => c.Session));
            }
        }

        public class When_an_event_requires_an_update_of_a_non_existing_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }), deferedExecution: true);
            }

            [Fact]
            public void Then_it_should_throw()
            {
                WhenAction.ShouldThrow<ProjectionException>();
            }
        }

        public class When_an_event_requires_an_update_of_a_cached_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_of_a_cached_projection()
            {
                Given(async () =>
                {
                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    The<LruProjectionCache>().Add(existingEntry);

                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public void Then_it_should_get_it_from_the_cache()
            {
                The<LruProjectionCache>().Hits.Should().Be(1);
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

        public class When_an_event_requires_an_update_of_a_unloaded_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_of_a_unloaded_projection()
            {
                Given(async () =>
                {
                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_update_the_database()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
            }

            [Fact]
            public async Task Then_it_should_add_it_to_cache()
            {
                ProductCatalogEntry entry = await The<LruProjectionCache>().Get<ProductCatalogEntry>(
                    "ProductCatalogEntry/c350E",
                    () =>
                    {
                        throw new InvalidOperationException("The entry should be cached.");
                    });

                entry.Id.Should().Be("ProductCatalogEntry/c350E");
                entry.Category.Should().Be("Hybrid");
            }
        }

        public class When_an_event_requires_an_update_if_exists_of_a_non_existing_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_if_exists_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateIfExistsOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_do_nothing()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_an_update_if_exists_of_a_cached_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_if_exists_of_a_cached_projection()
            {
                Given(async () =>
                {
                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    The<LruProjectionCache>().Add(existingEntry);

                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateIfExistsOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public void Then_it_should_get_it_from_the_cache()
            {
                The<LruProjectionCache>().Hits.Should().Be(1);
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

        public class When_an_event_requires_an_update_if_exists_of_a_unloaded_projection :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_if_exists_of_a_unloaded_projection()
            {
                Given(async () =>
                {
                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Gas"
                    };

                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateIfExistsOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                    {
                        await session.StoreAsync(existingEntry);
                        await session.SaveChangesAsync();
                    }

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_update_the_database()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();

                    entry.Category.Should().Be("Hybrid");
                }
            }

            [Fact]
            public async Task Then_it_should_add_it_to_cache()
            {
                ProductCatalogEntry entry = await The<LruProjectionCache>().Get<ProductCatalogEntry>(
                    "ProductCatalogEntry/c350E",
                    () =>
                    {
                        throw new InvalidOperationException("The entry should be cached.");
                    });

                entry.Id.Should().Be("ProductCatalogEntry/c350E");
                entry.Category.Should().Be("Hybrid");
            }
        }

        public class When_an_event_is_not_mapped_at_all : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_is_not_mapped_at_all()
            {
                Given(() =>
                {
                    The<LruProjectionCache>().Add(new ProductCatalogEntry
                    {
                        Id = "ProductCatalogEntry/c350E",
                        Category = "Hybrid"
                    });

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public void Then_it_should_not_do_anything()
            {
                The<LruProjectionCache>().Hits.Should().Be(0);
            }
        }

        public class When_a_custom_collection_name_is_set : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_a_custom_collection_name_is_set()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(e => e.ProductKey)
                        .Using((p, e, ctx) => p.Category = e.Category);

                    StartProjecting("CatalogEntries");
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public async Task Then_it_should_create_the_projection_under_that_collection()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("CatalogEntries/c350E");
                    entry.Should().NotBeNull();
                }
            }
        }

        public class When_an_event_has_a_header : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_an_event_has_a_header()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(anEvent => anEvent.ProductKey)
                        .Using((projection, anEvent, context) =>
                        {
                            projection.Category = anEvent.Category;
                            projection.AddedBy = (string)context.EventHeaders["UserName"];
                        });

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().WriteWithHeaders(
                    new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    },
                    new Dictionary<string, object>
                    {
                        ["UserName"] = "Pavel"
                    }));
            }

            [Fact]
            public async Task Then_it_should_use_the_header()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();
                    entry.AddedBy.Should().Be("Pavel");
                }
            }
        }

        public class When_a_transaction_has_a_header : Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_a_transaction_has_a_header()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(anEvent => anEvent.ProductKey)
                        .Using((projection, anEvent, context) =>
                        {
                            projection.Category = anEvent.Category;
                            projection.AddedBy = (string)context.TransactionHeaders["UserName"];
                        });

                    StartProjecting();
                });

                When(() => The<MemoryEventSource>().Write(
                    new Transaction
                    {
                        Events = new[]
                        {
                            new EventEnvelope
                            {
                                Body = new ProductAddedToCatalogEvent
                                {
                                    ProductKey = "c350E",
                                    Category = "Hybrid"
                                }
                            }
                        },
                        Headers = new Dictionary<string, object>
                        {
                            ["UserName"] = "Pavel"
                        }
                    }));
            }

            [Fact]
            public async Task Then_it_should_use_the_header()
            {
                using (var session = The<IDocumentStore>().OpenAsyncSession())
                {
                    var entry = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    entry.Should().NotBeNull();
                    entry.AddedBy.Should().Be("Pavel");
                }
            }
        }

        public class When_there_is_a_child_projector : Given_a_raven_projector_with_an_in_memory_event_source
        {
            private Transaction transaction2;
            private readonly List<ChildProjectionState> childProjectionStates = new List<ChildProjectionState>();

            public When_there_is_a_child_projector()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(anEvent => anEvent.ProductKey)
                        .Using((entry, anEvent) => entry.Category = anEvent.Category);

                    Events.Map<ProductAddedToCatalogEvent>()
                        .As(async (anEvent, context) =>
                        {
                            ProductCatalogChildEntry childEntry1 = await context.Session.LoadAsync<ProductCatalogChildEntry>(
                                "ProductCatalogChildEntry/c350E");

                            ProductCatalogChildEntry childEntry2 = await context.Session.LoadAsync<ProductCatalogChildEntry>(
                                "ProductCatalogChildEntry/c350F");

                            childProjectionStates.Add(new ChildProjectionState
                            {
                                Entry1Exists = childEntry1 != null,
                                Entry2Exists = childEntry2 != null
                            });
                        });

                    var childMapBuilder = new EventMapBuilder<ProductCatalogChildEntry, string, RavenProjectionContext>();

                    childMapBuilder.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(anEvent => anEvent.ProductKey)
                        .Using((entry, anEvent) => entry.Category = anEvent.Category);

                    var childProjector = new RavenChildProjector<ProductCatalogChildEntry>(childMapBuilder);

                    StartProjecting(children: new IRavenChildProjector[] { childProjector });
                });

                When(async () =>
                {
                    var transaction1 = new Transaction
                    {
                        Events = new[]
                        {
                            new EventEnvelope
                            {
                                Body = new ProductAddedToCatalogEvent
                                {
                                    ProductKey = "c350E",
                                    Category = "Hybrid"
                                }
                            }
                        }
                    };

                    transaction2 = new Transaction
                    {
                        Events = new[]
                        {
                            new EventEnvelope
                            {
                                Body = new ProductAddedToCatalogEvent
                                {
                                    ProductKey = "c350F",
                                    Category = "Gas"
                                }
                            }
                        }
                    };

                    await The<MemoryEventSource>().Write(transaction1, transaction2);
                });
            }

            [Fact]
            public async Task Then_the_parent_projector_should_project_all_the_transactions()
            {
                using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                {
                    ProductCatalogEntry parentEntry1 = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350E");
                    parentEntry1.Should().NotBeNull();
                    parentEntry1.Category.Should().Be("Hybrid");

                    ProductCatalogEntry parentEntry2 = await session.LoadAsync<ProductCatalogEntry>("ProductCatalogEntry/c350F");
                    parentEntry2.Should().NotBeNull();
                    parentEntry2.Category.Should().Be("Gas");
                }
            }

            [Fact]
            public async Task Then_the_child_projector_should_project_all_the_transactions()
            {
                using (IAsyncDocumentSession session = The<IDocumentStore>().OpenAsyncSession())
                {
                    ProductCatalogChildEntry childEntry1 =
                        await session.LoadAsync<ProductCatalogChildEntry>("ProductCatalogChildEntry/c350E");

                    childEntry1.Should().NotBeNull();
                    childEntry1.Category.Should().Be("Hybrid");

                    ProductCatalogChildEntry childEntry2 =
                        await session.LoadAsync<ProductCatalogChildEntry>("ProductCatalogChildEntry/c350F");

                    childEntry2.Should().NotBeNull();
                    childEntry2.Category.Should().Be("Gas");
                }
            }

            [Fact]
            public void Then_the_child_projector_should_process_each_transaction_before_the_parent_projector()
            {
                childProjectionStates[0].ShouldBeEquivalentTo(new ChildProjectionState
                {
                    Entry1Exists = true,
                    Entry2Exists = false
                });

                childProjectionStates[1].ShouldBeEquivalentTo(new ChildProjectionState
                {
                    Entry1Exists = true,
                    Entry2Exists = true
                });
            }

            private class ChildProjectionState
            {
                public bool Entry1Exists { get; set; }
                public bool Entry2Exists { get; set; }
            }
        }

        public class When_event_handling_fails :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            public When_event_handling_fails()
            {
                Given(() =>
                {
                    UseThe(new InvalidOperationException());

                    Events.Map<CategoryDiscontinuedEvent>().As(_ =>
                    {
                        throw The<InvalidOperationException>();
                    });

                    StartProjecting();

                    UseThe(new Transaction
                    {
                        Events = new[]
                        {
                            UseThe(new EventEnvelope
                            {
                                Body = new CategoryDiscontinuedEvent()
                            })
                        }
                    });
                });

                When(() => The<MemoryEventSource>().Write(The<Transaction>()), deferedExecution: true);
            }

            [Fact]
            public void Then_it_should_throw_projection_exception_with_the_inner_exception()
            {
                WhenAction.ShouldThrow<ProjectionException>()
                    .Which.InnerException.Should().BeSameAs(The<InvalidOperationException>());
            }

            [Fact]
            public void Then_it_should_identify_the_projector_via_the_projection_type()
            {
                WhenAction.ShouldThrow<ProjectionException>()
                    .Which.Projector.Should().Be(typeof(ProductCatalogEntry).ToString());
            }

            [Fact]
            public void Then_it_should_include_the_current_event()
            {
                WhenAction.ShouldThrow<ProjectionException>()
                    .Which.CurrentEvent.Should().BeSameAs(The<EventEnvelope>());
            }

            [Fact]
            public void Then_it_should_include_the_current_transaction_batch()
            {
                WhenAction.ShouldThrow<ProjectionException>()
                    .Which.TransactionBatch.Should().BeEquivalentTo(The<Transaction>());
            }
        }

        public class When_event_handling_fails_with_a_custom_exception_policy :
            Given_a_raven_projector_with_an_in_memory_event_source
        {
            private bool succeeded;
            private int numerOfFailedAttempts;
            private const int NumberOfTimesToFail = 3;

            public When_event_handling_fails_with_a_custom_exception_policy()
            {
                Given(() =>
                {
                    UseThe(new InvalidOperationException());

                    Events.Map<CategoryDiscontinuedEvent>().As((@event, context) =>
                    {
                        if (numerOfFailedAttempts < NumberOfTimesToFail)
                        {
                            throw The<InvalidOperationException>();
                        }

                        succeeded = true;
                    });

                    StartProjecting();

                    Subject.ShouldRetry = (exception, attempts) =>
                    {
                        return Task.Run(() =>
                        {
                            numerOfFailedAttempts = attempts;
                            if (attempts <= NumberOfTimesToFail)
                            {
                                return true;
                            }

                            return false;
                        });
                    };

                    UseThe(new Transaction
                    {
                        Events = new[]
                        {
                            UseThe(new EventEnvelope
                            {
                                Body = new CategoryDiscontinuedEvent()
                            })
                        }
                    });
                });

                When(() => The<MemoryEventSource>().Write(The<Transaction>()));
            }

            [Fact]
            public void Then_it_should_try_again()
            {
                numerOfFailedAttempts.Should().Be(NumberOfTimesToFail);
            }

            [Fact]
            public void Then_it_should_succeed()
            {
                succeeded.Should().BeTrue();
            }
        }
    }

    public class ProductCatalogEntry : IHaveIdentity
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string AddedBy { get; set; }
    }

    public class ProductCatalogChildEntry : IHaveIdentity
    {
        public string Id { get; set; }
        public string Category { get; set; }
    }

    public class ProductAddedToCatalogEvent
    {
        public string ProductKey { get; set; }
        public string Category { get; set; }
    }

    public class ProductMovedToCatalogEvent
    {
        public string ProductKey { get; set; }
        public string Category { get; set; }
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