using System.Collections.Generic;
using System.Threading.Tasks;
using eVision.QueryHost.Specs.Queries;

namespace eVision.QueryHost.Specs
{
    public class QuerySerializationSpecs
    {
        [Fact]
        public async Task When_submitting_a_query_that_returns_serializable_result_it_should_still_handle_the_query()
        {
            // http://stackoverflow.com/questions/12334382/net-webapi-serialization-k-backingfield-nastiness

            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var appBuilder = new AppBuilder();
            appBuilder.UseQueryHost(new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleWithSerializableQuery).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver()));

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(appBuilder.Build()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            ExampleWithSerializableQuery.Result result = await processor.Execute(new ExampleWithSerializableQuery
            {
                Values = new List<string> { "john", "mike", "jane" }
            });

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            result.Count.Should().Be(3);
            result.ConvertedValues.Should().BeEquivalentTo("JOHN", "MIKE", "JANE");
        }

        [Fact]
        public async Task When_submitting_a_query_that_returns_serializable_interface_result_it_should_still_handle_the_query()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var appBuilder = new AppBuilder(); 
            appBuilder.UseQueryHost(new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleWithSerializableInterfaceQuery).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver()));

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(appBuilder.Build()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            ExampleWithSerializableInterfaceQuery.Result result = await processor.Execute(new ExampleWithSerializableInterfaceQuery
            {
                Values = new List<string> { "john", "mike", "jane" }
            });

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            result.Count.Should().Be(3);
            result.ConvertedValues.Should().HaveCount(3);
        }
    }
}