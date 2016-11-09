using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Chill;

using FluentAssertions;

using FluentNHibernate.Mapping;
using LiquidProjections.Specs;
using NHibernate;
using NHibernate.Linq;

using Xunit;

namespace LiquidProjections.NHibernate.Specs
{
    namespace NHibernateProjectorSpecs
    {
        public class Given_a_sqlite_projector_with_an_in_memory_event_source : GivenWhenThen
        {
            protected readonly TaskCompletionSource<long> DispatchedCheckpointSource = new TaskCompletionSource<long>();
            protected NHibernateProjector<ProductCatalogEntry, string, ProjectorState> Projector;
            protected EventMapBuilder<ProductCatalogEntry, string, NHibernateProjectionContext> Events;

            public Given_a_sqlite_projector_with_an_in_memory_event_source()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    UseThe(new InMemorySQLiteDatabaseBuilder().Build());
                    UseThe(The<InMemorySQLiteDatabase>().SessionFactory);

                    Events = new EventMapBuilder<ProductCatalogEntry, string, NHibernateProjectionContext>();
                });
            }

            protected void StartProjecting()
            {
                Projector = new NHibernateProjector<ProductCatalogEntry, string, ProjectorState>(
                    The<ISessionFactory>().OpenSession, Events)
                {
                    BatchSize = 10
                };

                var dispatcher = new Dispatcher(The<MemoryEventSource>());

                dispatcher.Subscribe(0, async transactions =>
                {
                    await Projector.Handle(transactions);
                    DispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                });
            }
        }

        public class When_an_event_requires_a_create_of_a_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_create_of_a_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Gas"
                    };

                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(existingEntry);
                        session.Flush();
                    }

                    StartProjecting();
                });
            }

            [Fact]
            public async Task Then_it_should_throw()
            {
                await Assert.ThrowsAnyAsync<Exception>(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }
        }

        public class When_an_event_requires_a_create_of_a_new_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_of_a_new_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

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
            public async Task Then_it_should_create_a_new_projection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }

            [Fact]
            public void Then_it_should_track_the_transaction_checkpoint()
            {
                long? checkpoint = Projector.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_create_if_does_not_exist_of_a_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_if_does_not_exist_of_a_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateIfDoesNotExistOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Gas"
                    };

                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(existingEntry);
                        session.Flush();
                    }

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
            public async Task Then_it_should_do_nothing()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Gas");
                }
            }
        }

        public class When_an_event_requires_a_create_if_does_not_exist_of_a_new_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_if_does_not_exist_of_a_new_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateIfDoesNotExistOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

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
            public async Task Then_it_should_create_a_new_projection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }

            [Fact]
            public void Then_it_should_track_the_transaction_checkpoint()
            {
                long? checkpoint = Projector.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_create_or_update_of_a_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_or_update_of_a_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOrUpdateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Gas"
                    };

                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(existingEntry);
                        session.Flush();
                    }

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
            public async Task Then_it_should_update_the_projection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }
        }

        public class When_an_event_requires_a_create_or_update_of_a_new_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_a_create_or_update_of_a_new_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOrUpdateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

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
            public async Task Then_it_should_create_a_new_projection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }

            [Fact]
            public void Then_it_should_track_the_transaction_checkpoint()
            {
                long? checkpoint = Projector.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_an_update_of_a_non_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_an_update_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(productMovedToCatalogEvent => productMovedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productMovedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productMovedToCatalogEvent.Category);

                    StartProjecting();
                });
            }

            [Fact]
            public async Task Then_it_should_throw()
            {
                await Assert.ThrowsAsync<NHibernateProjectionException>(() =>
                    The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    }));
            }
        }

        public class When_an_event_requires_an_update_of_an_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_an_update_of_an_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(productMovedToCatalogEvent => productMovedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productMovedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productMovedToCatalogEvent.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Gas"
                    };

                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(existingEntry);
                        session.Flush();
                    }

                    StartProjecting();
                });

                When(async () =>
                {
                    transaction = await The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_update_the_projection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }

            [Fact]
            public void Then_it_should_track_the_transaction_checkpoint()
            {
                long? checkpoint = Projector.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_an_update_if_exists_of_a_non_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_an_update_if_exists_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateIfExistsOf(productMovedToCatalogEvent => productMovedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productMovedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productMovedToCatalogEvent.Category);

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
            public async Task Then_it_should_do_nothing()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_an_update_if_exists_of_an_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_an_update_if_exists_of_an_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateIfExistsOf(productMovedToCatalogEvent => productMovedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productMovedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productMovedToCatalogEvent.Category);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Gas"
                    };

                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(existingEntry);
                        session.Flush();
                    }

                    StartProjecting();
                });

                When(async () =>
                {
                    transaction = await The<MemoryEventSource>().Write(new ProductMovedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    });
                });
            }

            [Fact]
            public async Task Then_it_should_update_the_projection()
            {
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.Category.Should().Be("Hybrid");
                }
            }

            [Fact]
            public void Then_it_should_track_the_transaction_checkpoint()
            {
                long? checkpoint = Projector.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_delete_of_an_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_an_existing_projection()
            {
                Given(() =>
                {
                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(new ProductCatalogEntry
                        {
                            Id = "c350E"
                        });

                        session.Flush();
                    }

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(anEvent => anEvent.ProductKey);

                    StartProjecting();
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
            public void Then_it_should_remove_the_projection()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_a_delete_of_a_non_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductDiscontinuedEvent>().AsDeleteOf(e => e.ProductKey);

                    StartProjecting();
                });
            }

            [Fact]
            public Task Then_it_should_throw()
            {
                return Assert.ThrowsAsync<NHibernateProjectionException>(() =>
                    The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    }));
            }
        }

        public class When_an_event_requires_a_delete_of_a_modified_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_of_a_modified_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(productMovedToCatalogEvent => productMovedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productMovedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productMovedToCatalogEvent.Category);

                    Events.Map<ProductDiscontinuedEvent>()
                        .AsDeleteOf(productDiscontinuedEvent => productDiscontinuedEvent.ProductKey);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Gas"
                    };

                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(existingEntry);
                        session.Flush();
                    }

                    StartProjecting();
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(
                        new ProductMovedToCatalogEvent
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
            public void Then_it_should_remove_the_projection()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    var entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_a_delete_if_exists_of_an_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_an_existing_projection()
            {
                Given(() =>
                {
                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(new ProductCatalogEntry
                        {
                            Id = "c350E"
                        });

                        session.Flush();
                    }

                    Events.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(anEvent => anEvent.ProductKey);

                    StartProjecting();
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
            public void Then_it_should_remove_the_projection()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_a_delete_if_exists_of_a_non_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductDiscontinuedEvent>().AsDeleteIfExistsOf(e => e.ProductKey);

                    StartProjecting();
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
            public void Then_it_should_not_do_anything()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_a_delete_if_exists_of_a_modified_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_delete_if_exists_of_a_modified_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductMovedToCatalogEvent>()
                        .AsUpdateOf(productMovedToCatalogEvent => productMovedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productMovedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productMovedToCatalogEvent.Category);

                    Events.Map<ProductDiscontinuedEvent>()
                        .AsDeleteIfExistsOf(productDiscontinuedEvent => productDiscontinuedEvent.ProductKey);

                    var existingEntry = new ProductCatalogEntry
                    {
                        Id = "c350E",
                        Category = "Gas"
                    };

                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(existingEntry);
                        session.Flush();
                    }

                    StartProjecting();
                });

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(
                        new ProductMovedToCatalogEvent
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
            public void Then_it_should_remove_the_projection()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    var entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_requires_a_custom_action :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_requires_a_custom_action()
            {
                Given(() =>
                {
                    using (ISession session = The<ISessionFactory>().OpenSession())
                    {
                        session.Save(new ProductCatalogEntry
                        {
                            Id = "c350E",
                            Category = "Hybrids"
                        });

                        session.Flush();
                    }

                    Events.Map<CategoryDiscontinuedEvent>().As((categoryDiscontinuedEvent, context) =>
                    {
                        var entries = context.Session.Query<ProductCatalogEntry>()
                            .Where(entry => entry.Category == categoryDiscontinuedEvent.Category)
                            .ToList();

                        foreach (ProductCatalogEntry entry in entries)
                        {
                            context.Session.Delete(entry);
                        }

                        return Task.FromResult(false);
                    });

                    StartProjecting();
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

                using (var session = The<ISessionFactory>().OpenSession())
                {
                    var entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_an_event_is_not_mapped_at_all :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_is_not_mapped_at_all()
            {
                Given(() => StartProjecting());

                When(async () =>
                {
                    await The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrid"
                    });

                    await DispatchedCheckpointSource.Task;
                });
            }

            [Fact]
            public void Then_it_should_not_do_anything()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }
        }

        public class When_a_custom_state_key_is_set : GivenWhenThen
        {
            private readonly TaskCompletionSource<long> dispatchedCheckpointSource = new TaskCompletionSource<long>();
            private NHibernateProjector<ProductCatalogEntry, string, ProjectorState> projector;
            private Transaction transaction;
            private InMemorySQLiteDatabase database;

            public When_a_custom_state_key_is_set()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    database = new InMemorySQLiteDatabaseBuilder().Build();
                    UseThe(database.SessionFactory);

                    var events = new EventMapBuilder<ProductCatalogEntry, string, NHibernateProjectionContext>();

                    events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

                    projector = new NHibernateProjector<ProductCatalogEntry, string, ProjectorState>(
                        database.SessionFactory.OpenSession, events)
                    {
                        BatchSize = 10,
                        StateKey = "CatalogEntries"
                    };

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());

                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await projector.Handle(transactions);
                        dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
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
            public async Task Then_it_should_store_projector_state_with_that_key()
            {
                long lastCheckpoint = await dispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction.Checkpoint);

                using (var session = The<ISessionFactory>().OpenSession())
                {
                    ProjectorState projectorState = session.Get<ProjectorState>("CatalogEntries");
                    projectorState.Should().NotBeNull();
                }
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                database?.Dispose();
            }
        }

        public class When_an_event_has_a_header : Given_a_sqlite_projector_with_an_in_memory_event_source
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
            public void Then_it_should_use_the_header()
            {
                using (var session = The<ISessionFactory>().OpenSession())
                {
                    var entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.AddedBy.Should().Be("Pavel");
                }
            }
        }

        public class When_a_transaction_has_a_header : Given_a_sqlite_projector_with_an_in_memory_event_source
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
            public void Then_it_should_use_the_header()
            {
                using (var session = The<ISessionFactory>().OpenSession())
                {
                    var entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().NotBeNull();
                    entry.AddedBy.Should().Be("Pavel");
                }
            }
        }

        public class When_there_is_a_child_projector : GivenWhenThen
        {
            protected readonly TaskCompletionSource<long> DispatchedCheckpointSource = new TaskCompletionSource<long>();
            private Transaction transaction2;
            private readonly List<ChildProjectionState> childProjectionStates = new List<ChildProjectionState>();

            public When_there_is_a_child_projector()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    UseThe(new InMemorySQLiteDatabaseBuilder().Build());
                    UseThe(The<InMemorySQLiteDatabase>().SessionFactory);

                    var parentMapBuilder = new EventMapBuilder<ProductCatalogEntry, string, NHibernateProjectionContext>();

                    parentMapBuilder.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(anEvent => anEvent.ProductKey)
                        .Using((entry, anEvent) => entry.Category = anEvent.Category);

                    parentMapBuilder.Map<ProductAddedToCatalogEvent>().As((anEvent, context) =>
                    {
                        ProductCatalogChildEntry childEntry1 = context.Session.Get<ProductCatalogChildEntry>("c350E");
                        ProductCatalogChildEntry childEntry2 = context.Session.Get<ProductCatalogChildEntry>("c350F");

                        childProjectionStates.Add(new ChildProjectionState
                        {
                            Entry1Exists = childEntry1 != null,
                            Entry2Exists = childEntry2 != null
                        });
                    });

                    var childMapBuilder = new EventMapBuilder<ProductCatalogChildEntry, string, NHibernateProjectionContext>();

                    childMapBuilder.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(anEvent => anEvent.ProductKey)
                        .Using((entry, anEvent) => entry.Category = anEvent.Category);

                    var childProjector = new NHibernateChildProjector<ProductCatalogChildEntry, string>(childMapBuilder);

                    var parentProjector = new NHibernateProjector<ProductCatalogEntry, string, ProjectorState>(
                        The<ISessionFactory>().OpenSession,
                        parentMapBuilder,
                        new[] { childProjector })
                    {
                        BatchSize = 10
                    };

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());

                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await parentProjector.Handle(transactions);
                        DispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
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
                long lastCheckpoint = await DispatchedCheckpointSource.Task;
                lastCheckpoint.Should().Be(transaction2.Checkpoint);

                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry parentEntry1 = session.Get<ProductCatalogEntry>("c350E");
                    parentEntry1.Should().NotBeNull();
                    parentEntry1.Category.Should().Be("Hybrid");

                    ProductCatalogEntry parentEntry2 = session.Get<ProductCatalogEntry>("c350F");
                    parentEntry2.Should().NotBeNull();
                    parentEntry2.Category.Should().Be("Gas");
                }
            }

            [Fact]
            public void Then_the_child_projector_should_project_all_the_transactions()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogChildEntry childEntry1 = session.Get<ProductCatalogChildEntry>("c350E");
                    childEntry1.Should().NotBeNull();
                    childEntry1.Category.Should().Be("Hybrid");

                    ProductCatalogChildEntry childEntry2 = session.Get<ProductCatalogChildEntry>("c350F");
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
    }

    public class ProductCatalogEntry : IHaveIdentity<string>
    {
        public virtual string Id { get; set; }
        public virtual string Category { get; set; }
        public virtual string AddedBy { get; set; }
    }

    internal class ProductCatalogEntryClassMap : ClassMap<ProductCatalogEntry>
    {
        public ProductCatalogEntryClassMap()
        {
            Id(p => p.Id).Not.Nullable().Length(100);
            Map(p => p.Category).Nullable().Length(100);
            Map(p => p.AddedBy).Nullable().Length(100);
        }
    }

    public class ProductCatalogChildEntry : IHaveIdentity<string>
    {
        public virtual string Id { get; set; }
        public virtual string Category { get; set; }
    }

    internal class ProductCatalogChildEntryClassMap : ClassMap<ProductCatalogChildEntry>
    {
        public ProductCatalogChildEntryClassMap()
        {
            Id(p => p.Id).Not.Nullable().Length(100);
            Map(p => p.Category).Nullable().Length(100);
        }
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