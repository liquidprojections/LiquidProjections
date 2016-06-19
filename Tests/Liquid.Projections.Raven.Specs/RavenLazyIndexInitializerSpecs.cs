using System;
using System.Threading.Tasks;

namespace eVision.QueryHost.Raven.Specs
{
    namespace RavenLazyIndexInitializerSpecs
    {
        public class Given_embedded_database : GivenSubject<IDocumentStore>
        {
            public Given_embedded_database()
            {
                WithSubject(_ =>
                {
                    var documentStore = new EmbeddableDocumentStore
                    {
                        RunInMemory = true,
                        Configuration =
                        {
                            RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
                            RunInMemory = true
                        }
                    };

                    return documentStore.Initialize();
                });
            }
        }

        public class When_no_documents_have_been_stored_for_an_index : Given_embedded_database
        {
            private Task<IndexChangeNotification> changeInIndex;

            public When_no_documents_have_been_stored_for_an_index()
            {
                Given(() =>
                {
                    changeInIndex = Subject.Changes().ForAllIndexes().Take(1).ToTask();

                    new RavenLazyIndexInitializer()
                        .For<TestRavenProjection>().Add<TestRavenProjection_ByNameString>()
                        .SubscribeTo(Subject);
                });

                When(() =>
                {
                    using (var session = Subject.OpenSession())
                    {
                        session.SaveChanges();
                    }
                });
            }

            [Fact]
            public void Then_it_should_not_create_the_index_yet()
            {
                changeInIndex.Wait(1.Seconds()).Should().BeFalse();

                Subject.DatabaseCommands.GetIndexNames(0, 1).Should().BeEmpty();
            }
        }

        public class When_a_static_index_has_no_transform_result : Given_embedded_database
        {
            private Task<IndexChangeNotification> changeInIndex;

            public When_a_static_index_has_no_transform_result()
            {
                Given(() =>
                {
                    changeInIndex = Subject.Changes().ForAllIndexes().Take(1).ToTask();

                    Subject.Changes().ForAllIndexes().Subscribe(n =>
                    {
                        // NOTE: Temporary solution to see why this specs sometimes fails in TeamCity.
                        Console.WriteLine(n);
                    });

                    new RavenLazyIndexInitializer()
                        .For<TestRavenProjection>().Add<TestRavenProjection_ByName>()
                        .SubscribeTo(Subject);
                });

                When(() =>
                {
                    using (var session = Subject.OpenSession())
                    {
                        session.Store(new TestRavenProjection
                        {
                            Id = RavenSession.GetId<TestRavenProjection>("name"),
                            Name = "name"
                        });

                        session.SaveChanges();
                    }
                });
            }

            [Fact]
            public void Then_it_should_initialize_after_the_first_document_is_stored()
            {
                changeInIndex.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

                Subject.DatabaseCommands.GetIndexNames(start: 0, pageSize: 1).Should().NotBeEmpty();
            }
        }

        public class When_a_static_index_has_a_transform_result : Given_embedded_database
        {
            private Task<IndexChangeNotification> changeInIndex;

            public When_a_static_index_has_a_transform_result()
            {
                Given(() =>
                {
                    changeInIndex = Subject.Changes().ForAllIndexes().Take(1).ToTask();

                    new RavenLazyIndexInitializer()
                        .For<TestRavenProjection>().Add<TestRavenProjection_ByNameString>()
                        .SubscribeTo(Subject);
                });

                When(() =>
                {
                    using (var session = Subject.OpenSession())
                    {
                        session.Store(new TestRavenProjection
                        {
                            Id = RavenSession.GetId<TestRavenProjection>("name"),
                            Name = "name"
                        });

                        session.SaveChanges();
                    }
                });
            }

            [Fact]
            public void Then_it_should_initialize_after_the_first_document_is_stored()
            {
                changeInIndex.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

                Subject.DatabaseCommands.GetIndexNames(start: 0, pageSize: 1).Should().NotBeEmpty();
            }
        }

        internal class TestRavenProjection_ByName : AbstractIndexCreationTask<TestRavenProjection>
        {
            public TestRavenProjection_ByName()
            {
                Map = documents => from doc in documents
                    select new
                    {
                        doc.Name
                    };
            }
        }

        internal class TestRavenProjection_ByNameString : AbstractIndexCreationTask<TestRavenProjection, string>
        {
            public TestRavenProjection_ByNameString()
            {
                Map = documents => from doc in documents
                    select new
                    {
                        doc.Name
                    };
            }
        }
    }
}