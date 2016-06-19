using System;
using System.Collections.Generic;
using eVision.QueryHost.Raven.Dispatching;
using eVision.QueryHost.Raven.Specs.RavenSessionSpecs;
using FluentAssertions;
using Raven.Abstractions.Data;
using Raven.Client;
using Xunit;

namespace eVision.QueryHost.Raven.Specs
{
    public class DynamicStringDictionarySpecs
    {
        [Fact]
        public void When_converting_to_dictionary_it_should_succeed()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            dynamic subject = new DynamicStringDictionary();
            subject.Test1 = "val1";
            subject["Test2"] = "val2";

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            IDictionary<string, string> result = subject;

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            result.ShouldBeEquivalentTo(new Dictionary<string, string>
            {
                {"Test1", "val1"},
                {"Test2", "val2"}
            });
        }

        public class When_storing_to_ravendb : Given_a_test_raven_session_factory
        {
            private readonly TestDynamicProjection source = new TestDynamicProjection();
            private TestDynamicProjection result;

            public When_storing_to_ravendb()
            {
                Given(() =>
                {
                    source.TextProperties.Test1 = "val1";
                    source.TextProperties["Test2"] = "val2";
                });

                this.Given<TestRavenSessionFactory>(async () =>
                {
                    using (TestRavenSession session = Subject.Create())
                    {
                        await session.Store(source);
                        await session.SaveChanges();
                    }
                });

                When(async () =>
                {
                    using (TestRavenSession session = Subject.Create())
                    {
                        result = await session.Load<TestDynamicProjection>(source.Id);
                    }
                });
            }

            [Fact]
            public void It_should_have_same_text_properties()
            {
                IDictionary<string, string> fromSource = source.TextProperties;
                IDictionary<string, string> fromResult = result.TextProperties;
                fromResult.ShouldBeEquivalentTo(fromSource);
            }

            [Fact]
            public void It_should_not_be_the_same_class()
            {
                result.Should().NotBeSameAs(source);
                ((Dictionary<string, string>) result.TextProperties)
                    .Should()
                    .NotBeSameAs((Dictionary<string, string>) source.TextProperties);
            }
        }

        public class When_search_on_dynamic_properties : Given_a_test_raven_session_factory
        {
            private IList<TestDynamicProjection> result;

            public When_search_on_dynamic_properties()
            {
                this.Given<TestRavenSessionFactory>(async () =>
                {
                    using (TestRavenSession session = Subject.Create())
                    {
                        var first = new TestDynamicProjection();
                        first.TextProperties["prop1"] = "some1value";
                        first.TextProperties["prop2"] = "some2value";

                        var second = new TestDynamicProjection();
                        second.TextProperties["prop3"] = "some3value";
                        second.TextProperties["prop2"] = "some2value";

                        var third = new TestDynamicProjection();
                        third.TextProperties["prop1"] = "some1value";
                        third.TextProperties["prop3"] = "some3value";

                        await session.Store(first);
                        await session.Store(second);
                        await session.Store(third);

                        await session.SaveChanges();
                    }
                });

                When(async () =>
                {
                    using (TestRavenSession session = Subject.Create())
                    {
                        result = await session.Advanced.AsyncDocumentQuery<TestDynamicProjection>()
                            .NoTracking()
                            .WaitForNonStaleResults()
                            .UsingDefaultOperator(QueryOperator.And)
                            .Search("TextProperties.prop1", "*1*", EscapeQueryOptions.AllowAllWildcards)
                            .Search("TextProperties.prop2", "*2*", EscapeQueryOptions.AllowAllWildcards)
                            .ToListAsync();
                    }
                });
            }

            [Fact]
            public void It_should_return_single_item()
            {
                result.Should().HaveCount(1);
            }
        }

        public class TestDynamicProjection : IIdentity
        {
            public TestDynamicProjection()
            {
                Id = RavenSession.GetId<TestDynamicProjection>(Guid.NewGuid().ToString("N"));
                TextProperties = new DynamicStringDictionary();
            }

            public dynamic TextProperties { get; set; }
            public string Id { get; set; }
        }
    }
}