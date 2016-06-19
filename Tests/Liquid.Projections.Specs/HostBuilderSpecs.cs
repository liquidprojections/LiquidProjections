using System;
using System.Reflection;

namespace eVision.QueryHost.Specs
{
    namespace HostBuilderSpecs
    {
        public class When_an_assembly_contains_unmarked_queries : GivenSubject<QueryHostSettings>
        {
            private Assembly assemblyWithInvalidQueries;

            public When_an_assembly_contains_unmarked_queries()
            {
                Given(() => assemblyWithInvalidQueries = typeof(QueryWithoutApiName).Assembly);

                WhenAction = () => Subject.SupportingQueriesFrom(assemblyWithInvalidQueries);
            }

            [Fact]
            public void Then_it_should_throw()
            {
                WhenAction.ShouldThrow<ArgumentException>().WithMessage("*QueryWithoutApi*not*[ApiName]*");
            }
        }

        public class When_no_queries_have_been_found : GivenWhenThen
        {
            public When_no_queries_have_been_found()
            {
                QueryHostSettings settings = null;

                Given(() =>
                {
                    Assembly assemblyWithoutQueries = typeof(QueryHostSettings).Assembly;
        
                    settings = new QueryHostSettings()
                        .SupportingQueriesFrom(assemblyWithoutQueries);
                });

                WhenAction = () => QueryHostMiddleware.Create(settings);
            }

            [Fact]
            public void Then_it_should_throw()
            {
                WhenAction.ShouldThrow<InvalidOperationException>().WithMessage("No queries registered*");
            }
        }
    }
}