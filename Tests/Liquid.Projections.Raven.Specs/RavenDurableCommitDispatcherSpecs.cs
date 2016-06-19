using System.Threading.Tasks;

namespace eVision.QueryHost.Raven.Specs
{
    namespace RavenDurableCommitDispatcherSpecs
    {
        public class Given_an_in_memory_ravendb_and_event_store<TResult> :
            GivenSubject<DurableCommitDispatcher, TResult>
        {
            public Given_an_in_memory_ravendb_and_event_store()
            {
                Given(() =>
                {
                    UseThe(new MemoryEventSource());

                    UseThe(new EmbeddableDocumentStore
                    {
                        RunInMemory = true,
                        Configuration =
                        {
                            RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
                            RunInMemory = true
                        },
                        UseEmbeddedHttpServer = false
                    }.Initialize());
                });
            }
        }

        public class When_a_projector_is_registered_using_a_generic_factory :
            Given_an_in_memory_ravendb_and_event_store<Task<Transaction>>
        {
            public When_a_projector_is_registered_using_a_generic_factory()
            {
                Given(() =>
                {
                    UseThe(new TestRavenSessionFactory(The<IDocumentStore>()));

                    The<MemoryEventSource>().Write(new TestEvent
                    {
                        Name = "test",
                        Version = 2
                    });
                });

                WithSubject(_ =>
                {
                    return new RavenDurableCommitDispatcherBuilder()
                        .ListeningTo(The<MemoryEventSource>())
                        .Named("test/1")
                        .ResolvesSession(The<TestRavenSessionFactory>().Create)
                        .WithProjector(session => new TestRavenProjector(session))
                        .Build();
                });

                When(async () =>
                {
                    Task<Transaction> commitProjected = Subject.DispatchedCommits.Take(1).ToTask();

                    await Subject.Start();

                    return commitProjected;
                });
            }

            [Fact]
            public async Task It_should_dispatch_an_event_to_that_projector()
            {
                Subject.PollNow();

                await Result;

                using (var session = The<TestRavenSessionFactory>().Create())
                {
                    var projection = await session.Load<TestRavenProjection>("test");
                    projection.Id.Should().Be(RavenSession.GetId<TestRavenProjection>("test"));
                    projection.Name.Should().Be("test");
                    projection.Version.Should().Be(2);
                }
            }
        }

        public class When_a_projector_is_registered_through_a_register :
            Given_an_in_memory_ravendb_and_event_store<Task<Transaction>>
        {
            public When_a_projector_is_registered_through_a_register()
            {
                Given(() =>
                {
                    UseThe(new TestRavenSessionFactory(The<IDocumentStore>()));

                    The<MemoryEventSource>().Write(new TestEvent
                    {
                        Name = "test",
                        Version = 2
                    });
                });

                WithSubject(_ =>
                {
                    var projectorRegister = new ProjectorRegistry<IWritableRavenSession>();
                    projectorRegister.Add(typeof (TestRavenProjector),
                        (type, session) => new TestRavenProjector(session));

                    return new RavenDurableCommitDispatcherBuilder()
                        .ListeningTo(The<MemoryEventSource>())
                        .Named("test/1")
                        .ResolvesSession(The<TestRavenSessionFactory>().Create)
                        .WithProjectors(projectorRegister)
                        .Build();
                });

                When(async () =>
                {
                    Task<Transaction> commitProjected = Subject.DispatchedCommits.Take(1).ToTask();

                    await Subject.Start();

                    return commitProjected;
                });
            }

            [Fact]
            public async Task It_should_dispatch_an_event_to_that_projector()
            {
                Subject.PollNow();

                await Result;

                using (var session = The<TestRavenSessionFactory>().Create())
                {
                    var projection = await session.Load<TestRavenProjection>("test");
                    projection.Id.Should().Be(RavenSession.GetId<TestRavenProjection>("test"));
                    projection.Name.Should().Be("test");
                    projection.Version.Should().Be(2);
                }
            }
        }

        public class When_a_projector_with_lookup_is_registered_through_a_register :
            Given_an_in_memory_ravendb_and_event_store<Task<Transaction>>
        {
            public When_a_projector_with_lookup_is_registered_through_a_register()
            {
                Given(() =>
                {
                    UseThe(new TestRavenSessionFactory(The<IDocumentStore>()));

                    The<MemoryEventSource>().Write(new TestEvent
                    {
                        Name = "test",
                        LookupProperty = "lookup1",
                        Version = 2
                    });
                });

                WithSubject(_ =>
                {
                    var projectorRegister = new ProjectorRegistry<IWritableRavenSession>();
                    projectorRegister.Add(session => new TestRavenProjectorWithLookup(session));

                    return new RavenDurableCommitDispatcherBuilder()
                        .ListeningTo(The<MemoryEventSource>())
                        .Named("test/1")
                        .ResolvesSession(The<TestRavenSessionFactory>().Create)
                        .WithProjectors(projectorRegister)
                        .Build();
                });

                When(async () =>
                {
                    Task<Transaction> commitProjected = Subject.DispatchedCommits.Take(1).ToTask();

                    await Subject.Start();

                    return commitProjected;
                });
            }

            [Fact]
            public async Task It_should_dispatch_an_event_to_that_projector()
            {
                Subject.PollNow();

                await Result;

                using (var session = The<TestRavenSessionFactory>().Create())
                {
                    var projection = await session.Load<TestRavenProjection>("test");
                    projection.Id.Should().Be(RavenSession.GetId<TestRavenProjection>("test"));
                    projection.Name.Should().Be("test");
                    projection.Version.Should().Be(2);
                }
            }

            [Fact]
            public async Task It_should_dispatch_an_event_to_update_lookup()
            {
                Subject.PollNow();

                await Result;

                using (var session = The<TestRavenSessionFactory>().Create())
                {
                    var lookup = await session.Load<TestRavenProjectionLookup>("lookup1");
                    lookup.Id.Should().Be(RavenSession.GetId<TestRavenProjectionLookup>("lookup1"));
                    lookup.ProjectionIds.Should().BeEquivalentTo(RavenSession.GetId<TestRavenProjection>("test"));
                }
            }
        }

        public class When_a_projector_with_lookup_is_registered_and_lookup_is_changed :
            Given_an_in_memory_ravendb_and_event_store<Task<Transaction>>
        {
            public When_a_projector_with_lookup_is_registered_and_lookup_is_changed()
            {
                Given(() =>
                {
                    UseThe(new TestRavenSessionFactory(The<IDocumentStore>()));

                    The<MemoryEventSource>().Write(new TestEvent
                    {
                        Name = "test",
                        LookupProperty = "lookup1",
                        Version = 2
                    });

                    The<MemoryEventSource>().Write(new TestLookupChangedEvent
                    {
                        Name = "test",
                        LookupProperty = "lookup2",
                        Version = 3
                    });
                });

                WithSubject(_ =>
                {
                    var projectorRegister = new ProjectorRegistry<IWritableRavenSession>();
                    projectorRegister.Add(session => new TestRavenProjectorWithLookup(session));

                    return new RavenDurableCommitDispatcherBuilder()
                        .ListeningTo(The<MemoryEventSource>())
                        .Named("test/1")
                        .ResolvesSession(The<TestRavenSessionFactory>().Create)
                        .WithProjectors(projectorRegister)
                        .Build();
                });

                When(async () =>
                {
                    Task<Transaction> commitProjected = Subject.DispatchedCommits.Skip(1).Take(1).ToTask();

                    await Subject.Start();

                    return commitProjected;
                });
            }

            [Fact]
            public async Task It_should_dispatch_an_event_to_that_projector()
            {
                Subject.PollNow();

                await Result;

                using (var session = The<TestRavenSessionFactory>().Create())
                {
                    var projection = await session.Load<TestRavenProjection>("test");
                    projection.Id.Should().Be(RavenSession.GetId<TestRavenProjection>("test"));
                    projection.Name.Should().Be("test");
                    projection.Version.Should().Be(3);
                }
            }

            [Fact]
            public async Task It_should_remove_old_update_lookup()
            {
                Subject.PollNow();

                await Result;

                using (var session = The<TestRavenSessionFactory>().Create())
                {
                    var lookup = await session.Load<TestRavenProjectionLookup>("lookup1");
                    lookup.Should().BeNull();
                }
            }

            [Fact]
            public async Task It_should_dispatch_update_lookup()
            {
                Subject.PollNow();

                await Result;

                using (var session = The<TestRavenSessionFactory>().Create())
                {
                    var lookup = await session.Load<TestRavenProjectionLookup>("lookup2");
                    lookup.Id.Should().Be(RavenSession.GetId<TestRavenProjectionLookup>("lookup2"));
                    lookup.ProjectionIds.Should().BeEquivalentTo(RavenSession.GetId<TestRavenProjection>("test"));
                }
            }
        }
    }
}