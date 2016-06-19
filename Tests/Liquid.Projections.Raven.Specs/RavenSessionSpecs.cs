using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace eVision.QueryHost.Raven.Specs
{
    namespace RavenSessionSpecs
    {
        public class Given_a_test_raven_session_factory : GivenSubject<TestRavenSessionFactory>
        {
            public Given_a_test_raven_session_factory()
            {
                Given(() =>
                {
                    SetThe<IDocumentStore>().To(new EmbeddableDocumentStore
                    {
                        RunInMemory = true,
                        Configuration =
                        {
                            RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
                            RunInMemory = true
                        }
                    }.Initialize());

                    WithSubject(_ => new TestRavenSessionFactory(The<IDocumentStore>()));
                });
            }

            protected override void Dispose(bool disposing)
            {
                The<IDocumentStore>().Dispose();
                base.Dispose(disposing);
            }
        }

        public class When_storing_entity : Given_a_test_raven_session_factory
        {
            private readonly string Id = Guid.NewGuid().ToString("N");

            public When_storing_entity()
            {
                Given(() => { });

                When(async () =>
                {
                    using (var writer = Subject.Create())
                    {
                        await writer.Store(new TestRavenProjection
                        {
                            Id = RavenSession.GetId<TestRavenProjection>(Id)
                        });
                        await writer.SaveChanges();
                    }
                });
            }

            [Fact]
            public async Task It_should_persist_it()
            {
                TestRavenProjection result;

                using (var writer = Subject.Create())
                {
                    result = await writer.Load<TestRavenProjection>(Id);
                }

                result.Should().NotBeNull();
                result.Id.Should().Be(RavenSession.GetId<TestRavenProjection>(Id));
            }
        }

        public class When_deleting_entity : Given_a_test_raven_session_factory
        {
            private readonly string Id = Guid.NewGuid().ToString("N");

            public When_deleting_entity()
            {
                this.Given<TestRavenSessionFactory>(async () =>
                {
                    using (var writer = Subject.Create())
                    {
                        await writer.Store(new TestRavenProjection
                        {
                            Id = RavenSession.GetId<TestRavenProjection>(Id)
                        });
                        await writer.SaveChanges();
                    }
                });

                When(async () =>
                {
                    using (var writer = Subject.Create())
                    {
                        var toDelete = await writer.Load<TestRavenProjection>(Id);
                        await writer.Delete(toDelete);
                        await writer.SaveChanges();
                    }
                });
            }

            [Fact]
            public async Task It_should_remove_it()
            {
                TestRavenProjection result;

                using (var writer = Subject.Create())
                {
                    result = await writer.Load<TestRavenProjection>(Id);
                }

                result.Should().BeNull();
            }
        }

        public class When_streaming_many_entities : Given_a_test_raven_session_factory
        {
            private IEnumerable<TestRavenProjection> result;
            private const int TotalCount = 1000;
            private const int StartFrom = 10;
            private const int TakeTill = 1000;

            public When_streaming_many_entities()
            {
                Given(() => new TestRavenProjection_ByVersion().Execute(The<IDocumentStore>()));

                this.Given<TestRavenSessionFactory>(async () =>
                {
                    using (IWritableRavenSession writer = Subject.CreateBulkWriter())
                    {
                        Enumerable.Range(1, TotalCount).ForEach(async x =>
                        {
                            await writer.Store(new TestRavenProjection
                            {
                                Id = RavenSession.GetId<TestRavenProjection>(x.ToString(CultureInfo.InvariantCulture)),
                                Version = x
                            });

                            await writer.SaveChanges();
                        });
                    }

                    using (var session = Subject.Create())
                    {
                        // workaround for the 'last write' to work, since bulk insert does not change the last write.
                        await session.Store(new TestRavenProjection
                        {
                            Id = RavenSession.GetId<TestRavenProjection>("0"),
                            Version = 0
                        });

                        await session.SaveChanges();
                    }
                });

                When(async () =>
                {
                    using (TestRavenSession reader = Subject.Create())
                    {
                        Func<IRavenQueryable<TestRavenProjection>, IQueryable<TestRavenProjection>> query = q => q
                            .Where(x => (x.Version >= StartFrom) && (x.Version < TakeTill))
                            .OrderBy(x => x.Version);

                        result = await reader.Stream<TestRavenProjection, TestRavenProjection_ByVersion>(query);
                    }
                });
            }

            [Fact]
            public void It_should_return_all_the_count_of_results_requested()
            {
                result.Should().HaveCount(TakeTill - StartFrom);
            }

            [Fact]
            public void It_should_match_all_the_results()
            {
                var expected = Enumerable.Range(StartFrom, TakeTill - StartFrom)
                    .Select(x => RavenSession.GetId<TestRavenProjection>(x.ToString(CultureInfo.InvariantCulture)))
                    .ToArray();
                var resultIds = result.Select(x => x.Id).ToArray();

                expected.Except(resultIds).Should().BeEmpty();
                resultIds.Should().BeEquivalentTo(expected);
            }
        }

        internal class TestRavenProjection_ByVersion : AbstractIndexCreationTask<TestRavenProjection>
        {
            public TestRavenProjection_ByVersion()
            {
                Map = documents => from doc in documents
                    select new
                    {
                        doc.Version
                    };
                // resuires Long to search on ranges (don't know exactly why)
                Sort(x => x.Version, SortOptions.Long);
            }
        }
    }
}