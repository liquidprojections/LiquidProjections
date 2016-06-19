using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using eVision.QueryHost.Client;
using eVision.QueryHost.InvalidQueriesForTests;
using eVision.QueryHost.Specs.Queries;
using FluentAssertions;
using Microsoft.Owin.Builder;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Xunit;

namespace eVision.QueryHost.Specs
{
    public class HttpQueryProcessorSpecs
    {
        [Fact]
        public async Task When_submitting_a_valid_query_in_process_it_should_return_the_result_correctly()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var appBuilder = new AppBuilder();
            appBuilder.UseQueryHost(new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver()));

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(appBuilder.Build()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Result result = await processor.Execute(new ExampleQuery
            {
                Values = new List<string> {"john", "mike", "jane"}
            });

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            result.Count.Should().Be(3);
            result.ConvertedValues.Should().BeEquivalentTo("JOHN", "MIKE", "JANE");
        }

        [Fact]
        [Trait("Category", "Network")]
        public async Task When_submitting_a_valid_query_over_the_network_it_should_return_the_result_correctly()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (WebApp.Start("http://localhost:9999/", appBuilder => appBuilder.UseQueryHost(settings)))
            {
                IQueryProcessor processor = new HttpQueryProcessor(new Uri("http://localhost:9999/"));

                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                Result result = await processor.Execute(new ExampleQuery
                {
                    Values = new List<string> {"john", "mike", "jane"}
                });

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                result.Count.Should().Be(3);
                result.ConvertedValues.Should().BeEquivalentTo("JOHN", "MIKE", "JANE");
            }
        }

        [Fact]
        [Trait("Category", "Network")]
        public void When_the_host_cannot_be_reached_over_the_network_it_should_throw_a_clear_exception()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            IQueryProcessor processor = new HttpQueryProcessor(new Uri("http://localhost:9999/"));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Func<Task> act = async () => await processor.Execute(new ExampleQuery
            {
                Values = new List<string> {"john", "mike", "jane"}
            });

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            act.ShouldThrow<HttpRequestException>();
        }

        [Fact]
        public void When_submitting_a_query_the_remote_processor_cannot_handle_it_should_throw_a_clear_exception()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var appBuilder = new AppBuilder();
            appBuilder.UseQueryHost(new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver()));

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(appBuilder.Build()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Func<Task> act = async () => { await processor.Execute(new MissingHandlerQuery()); };

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            act.ShouldThrow<QueryException>()
                .WithMessage("*501*how to handle query 'MissingHandlerQuery'*");
        }
        
        [Fact]
        public void When_submitting_a_query_over_the_network_the_remote_processor_cannot_handle_it_should_throw_a_clear_exception()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (WebApp.Start("http://localhost:9999/", appBuilder => appBuilder.UseQueryHost(settings)))
            {
                IQueryProcessor processor = new HttpQueryProcessor(new Uri("http://localhost:9999"));

                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                Func<Task> act = async () => { await processor.Execute(new MissingHandlerQuery()); };

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                act.ShouldThrow<QueryException>()
                    .WithMessage("*501*how to handle query 'MissingHandlerQuery'*");
            }
        }

        [Fact]
        public void When_submitting_an_unserializable_query_it_should_throw_a_clear_exception()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(settings.ToAppFunc()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Func<Task> act = async () => { await processor.Execute(new UnserializableQuery()); };

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            act.ShouldThrow<JsonSerializationException>()
                .WithMessage("*UnserializableQuery*");
        }

        [Fact]
        public void When_query_handler_throws_a_non_http_exception_it_should_return_a_500_code()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var appBuilder = new AppBuilder();
            appBuilder.UseQueryHost(new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver()));

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(appBuilder.Build()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Func<Task> act = async () => { await processor.Execute(new ThrowingQuery()); };

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            act.ShouldThrow<QueryException>()
                .WithMessage("*internal*error*");
        }
        
        [Fact]
        public void When_query_handler_throws_a_non_http_exception_over_network_it_should_return_a_500_code()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());
            
            using (WebApp.Start("http://localhost:9999/", appBuilder => appBuilder.UseQueryHost(settings)))
            {
                IQueryProcessor processor = new HttpQueryProcessor(new Uri("http://localhost:9999"));

                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                Func<Task> act = async () => { await processor.Execute(new ThrowingQuery()); };

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                act.ShouldThrow<QueryException>()
                    .WithMessage("*internal*error*");
            }
        }

        [Fact]
        public void When_query_handler_throws_a_business_exception_it_should_retain_the_exception_type()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var appBuilder = new AppBuilder();
            appBuilder.UseQueryHost(new QueryHostSettings()
                .WithBusinessExceptionType<SomeBusinessException>()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver()));

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(appBuilder.Build()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Func<Task> act = async () => { await processor.Execute(new ThrowingQuery()); };

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            act.ShouldThrow<SomeBusinessException>()
                .WithMessage("*Invalid operation*");
        }
        
        [Fact]
        public void When_query_handler_throws_a_business_exception_over_network_it_should_retain_the_exception_type()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .WithBusinessExceptionType<SomeBusinessException>()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (WebApp.Start("http://localhost:9999/", appBuilder => appBuilder.UseQueryHost(settings)))
            {
                IQueryProcessor processor = new HttpQueryProcessor(new Uri("http://localhost:9999"));

                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                Func<Task> act = async () => { await processor.Execute(new ThrowingQuery()); };

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                act.ShouldThrow<SomeBusinessException>()
                    .WithMessage("*Invalid operation*");
            }
        }

        [Fact]
        public void When_query_handler_throws_with_an_http_error_code_it_should_pass_it_to_the_caller()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var appBuilder = new AppBuilder();
            appBuilder.UseQueryHost(new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver()));

            IQueryProcessor processor = new HttpQueryProcessor(new OwinHttpMessageHandler(appBuilder.Build()));

            //-----------------------------------------------------------------------------------------------------------
            // Act
            //-----------------------------------------------------------------------------------------------------------
            Func<Task> act = async () =>
            {
                await processor.Execute(new HttpThrowingQuery
                {
                    Status = HttpStatusCode.BadRequest,
                    Message = "The request was bad"
                });
            };

            //-----------------------------------------------------------------------------------------------------------
            // Assert
            //-----------------------------------------------------------------------------------------------------------
            act.ShouldThrow<QueryException>()
                .Where(e => (e.Status == HttpStatusCode.BadRequest) && e.Message.Contains("was bad"));
        }
        
        [Fact]
        public void When_query_handler_throws_an_http_error_code_over_network_it_should_pass_it_to_the_caller()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (WebApp.Start("http://localhost:9999/", appBuilder => appBuilder.UseQueryHost(settings)))
            {
                IQueryProcessor processor = new HttpQueryProcessor(new Uri("http://localhost:9999"));

                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                Func<Task> act = async () =>
                {
                    await processor.Execute(new HttpThrowingQuery
                    {
                        Status = HttpStatusCode.BadRequest,
                        Message = "The request was bad"
                    });
                };

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                act.ShouldThrow<QueryException>()
                    .Where(e => (e.Status == HttpStatusCode.BadRequest) && e.Message.Contains("was bad"));
            }
        }
    }
}