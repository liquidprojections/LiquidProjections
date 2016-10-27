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
        public class Given_a_sqlite_projector_with_an_in_memory_event_source : GivenWhenThen
        {
            protected readonly TaskCompletionSource<long> DispatchedCheckpointSource = new TaskCompletionSource<long>();
            protected NHibernateProjector<ProductCatalogEntry, ProjectorState> Projector;
            protected EventMapBuilder<ProductCatalogEntry, NHibernateProjectionContext> Events;

            private InMemorySQLiteDatabase database;

            public Given_a_sqlite_projector_with_an_in_memory_event_source()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    database = new InMemorySQLiteDatabaseBuilder().Build();
                    UseThe(database.SessionFactory);

                    Events = new EventMapBuilder<ProductCatalogEntry, NHibernateProjectionContext>();

                    Projector = new NHibernateProjector<ProductCatalogEntry, ProjectorState>(
                        database.SessionFactory.OpenSession, Events, batchSize: 10);

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());

                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await Projector.Handle(transactions);
                        DispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });
                });
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                database?.Dispose();
            }
        }

        public class When_an_event_requires_an_update_of_a_non_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            private Transaction transaction;

            public When_an_event_requires_an_update_of_a_non_existing_projection()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                            productCatalogEntry.Category = productAddedToCatalogEvent.Category);
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

        public class When_an_unloaded_projection_must_be_deleted :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_unloaded_projection_must_be_deleted()
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

        public class When_an_event_deletes_non_existing_projection :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_an_event_deletes_non_existing_projection()
            {
                Given(() =>
                {
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
            public void Then_it_should_not_do_anything()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
                {
                    ProductCatalogEntry entry = session.Get<ProductCatalogEntry>("c350E");
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

        public class When_a_modified_projection_must_be_deleted :
            Given_a_sqlite_projector_with_an_in_memory_event_source
        {
            public When_a_modified_projection_must_be_deleted()
            {
                Given(() =>
                {
                    Events.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);

                    Events.Map<ProductDiscontinuedEvent>()
                        .AsDeleteOf(productDiscontinuedEvent => productDiscontinuedEvent.ProductKey);
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
            public void Then_it_should_remove_the_projection()
            {
                using (ISession session = The<ISessionFactory>().OpenSession())
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
            private NHibernateProjector<ProductCatalogEntry, ProjectorState> projector;
            private Transaction transaction;
            private InMemorySQLiteDatabase database;

            public When_a_custom_state_key_is_set()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    database = new InMemorySQLiteDatabaseBuilder().Build();
                    UseThe(database.SessionFactory);

                    var events = new EventMapBuilder<ProductCatalogEntry, NHibernateProjectionContext>();

                    projector = new NHibernateProjector<ProductCatalogEntry, ProjectorState>(
                        database.SessionFactory.OpenSession, events, batchSize: 10, stateKey: "CatalogEntries");

                    var dispatcher = new Dispatcher(The<MemoryEventSource>());

                    dispatcher.Subscribe(0, async transactions =>
                    {
                        await projector.Handle(transactions);
                        dispatchedCheckpointSource.SetResult(transactions.Last().Checkpoint);
                    });

                    events.Map<ProductAddedToCatalogEvent>()
                        .AsUpdateOf(productAddedToCatalogEvent => productAddedToCatalogEvent.ProductKey)
                        .Using((productCatalogEntry, productAddedToCatalogEvent, context) =>
                                productCatalogEntry.Category = productAddedToCatalogEvent.Category);
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
    }

    public class ProductCatalogEntry : IHaveIdentity
    {
        public virtual string Id { get; set; }
        public virtual string Category { get; set; }
    }

    internal class ProductCatalogEntryClassMap : ClassMap<ProductCatalogEntry>
    {
        public ProductCatalogEntryClassMap()
        {
            Id(p => p.Id).Not.Nullable().Length(100);
            Map(p => p.Category).Nullable().Length(100);
        }
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