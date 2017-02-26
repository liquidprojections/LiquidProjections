using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Chill;

using FluentAssertions;

using FluentNHibernate.Mapping;

using NHibernate;
using NHibernate.Linq;

using Xunit;

namespace LiquidProjections.NHibernate.Specs
{
    namespace NHibernateProjectorSpecs
    {
        public class Given_a_sqlite_projector_with_an_in_memory_event_source :
            GivenSubject<NHibernateProjector<ProductCatalogEntry, string, ProjectorState>>
        {
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

            protected void StartProjecting(string stateKey = null, INHibernateChildProjector[] children = null)
            {
                WithSubject(_ => new NHibernateProjector<ProductCatalogEntry, string, ProjectorState>(
                    The<ISessionFactory>().OpenSession, Events, children)
                {
                    BatchSize = 10
                });

                if (!string.IsNullOrEmpty(stateKey))
                {
                    Subject.StateKey = stateKey;
                }

                The<MemoryEventSource>().Subscribe(0, Subject.Handle, "");
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
            public void Then_it_should_create_a_new_projection()
            {
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
                long? checkpoint = Subject.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_create_if_does_not_exist_of_a_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
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

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public void Then_it_should_do_nothing()
            {
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
            public void Then_it_should_create_a_new_projection()
            {
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
                long? checkpoint = Subject.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_a_create_or_update_of_a_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
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

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public void Then_it_should_update_the_projection()
            {
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
            public void Then_it_should_create_a_new_projection()
            {
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
                long? checkpoint = Subject.GetLastCheckpoint();
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
            public void Then_it_should_update_the_projection()
            {
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
                long? checkpoint = Subject.GetLastCheckpoint();
                checkpoint.Should().Be(transaction.Checkpoint);
            }
        }

        public class When_an_event_requires_an_update_if_exists_of_a_non_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
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

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public void Then_it_should_do_nothing()
            {
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
            public void Then_it_should_update_the_projection()
            {
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
                long? checkpoint = Subject.GetLastCheckpoint();
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

                When(() => The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }));
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

                When(() => The<MemoryEventSource>().Write(
                    new ProductMovedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrids"
                    }, new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    }));
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

                When(() => The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }));
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

                When(() => The<MemoryEventSource>().Write(new ProductDiscontinuedEvent
                {
                    ProductKey = "c350E",
                }));
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

                When(() => The<MemoryEventSource>().Write(
                    new ProductMovedToCatalogEvent
                    {
                        ProductKey = "c350E",
                        Category = "Hybrids"
                    }, new ProductDiscontinuedEvent
                    {
                        ProductKey = "c350E",
                    }));
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
            private NHibernateProjectionContext context;

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

                        this.context = context;

                        return Task.FromResult(false);
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
                                    Category = "Hybrids"
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
            public void Then_it_should_have_executed_the_custom_action()
            {
                using (var session = The<ISessionFactory>().OpenSession())
                {
                    var entry = session.Get<ProductCatalogEntry>("c350E");
                    entry.Should().BeNull();
                }
            }

            [Fact]
            public void Then_it_should_have_created_the_context()
            {
                context.ShouldBeEquivalentTo(new NHibernateProjectionContext
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

        public class When_an_event_is_not_mapped_at_all :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_is_not_mapped_at_all()
            {
                Given(() => StartProjecting());

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
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

        public class When_a_custom_state_key_is_set : Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_a_custom_state_key_is_set()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsCreateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

                    StartProjecting(stateKey: "CatalogEntries");
                });

                When(() => The<MemoryEventSource>().Write(new ProductAddedToCatalogEvent
                {
                    ProductKey = "c350E",
                    Category = "Hybrid"
                }));
            }

            [Fact]
            public void Then_it_should_store_projector_state_with_that_key()
            {
                using (var session = The<ISessionFactory>().OpenSession())
                {
                    ProjectorState projectorState = session.Get<ProjectorState>("CatalogEntries");
                    projectorState.Should().NotBeNull();
                }
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

        public class When_there_is_a_child_projector :
            Given_a_sqlite_projector_with_an_in_memory_event_source
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

                    Events.Map<ProductAddedToCatalogEvent>().As((anEvent, context) =>
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

                    StartProjecting(children: new INHibernateChildProjector[] { childProjector });
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
            public void Then_the_parent_projector_should_project_all_the_transactions()
            {
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

        public class When_event_handling_fails :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_event_handling_fails()
            {
                Given(() =>
                {
                    UseThe(new InvalidOperationException());

                    Events.Map<CategoryDiscontinuedEvent>().As((@event, context) =>
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