using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using eVision.QueryHost.Specs.Queries;

namespace eVision.QueryHost.Specs
{
    public class QueryHostMiddlewareSpecs
    {
        [Fact]
        public async Task When_submitting_a_valid_query_it_should_return_the_result_correctly()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (var server = TestServer.Create(app => app.UseQueryHost(settings)))
            {
                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                string body = JsonConvert.SerializeObject(new ExampleQuery
                {
                    Values = new List<string> { "SomeValue" }
                });

                HttpResponseMessage response =
                    await server.HttpClient.PostAsync(Constants.RoutePrefix + "/" + Constants.QueryRoute + "/example",
                        new StringContent(body, Encoding.UTF8, "application/json"));

                string content = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<Result>(content);

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                result.ConvertedValues.Should().NotBeEmpty();
            }
        }

        [Fact]
        public async Task When_an_alternative_route_has_been_specified_it_should_accept_request_on_it()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .UsingRoutePrefix("myqueries")
                .SupportingQueriesFrom(typeof(ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (var server = TestServer.Create(app => app.UseQueryHost(settings)))
            {
                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                string body = JsonConvert.SerializeObject(new ExampleQuery
                {
                    Values = new List<string> { "john", "mike", "jane" }
                });

                HttpResponseMessage response =
                    await server.HttpClient.PostAsync("/myqueries/" + Constants.QueryRoute + "/example",
                        new StringContent(body, Encoding.UTF8, "application/json"));

                string content = await response.Content.ReadAsStringAsync();

                var result = JsonConvert.DeserializeObject<Result>(content);

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                result.Count.Should().Be(3);
                result.ConvertedValues.Should().BeEquivalentTo("JOHN", "MIKE", "JANE");
            }
        }

        [Fact]
        public async Task When_submitting_an_unknown_query_it_should_return_the_correct_response_code()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (var server = TestServer.Create(app => app.UseQueryHost(settings)))
            {
                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                string body = JsonConvert.SerializeObject(new ExampleQuery());

                HttpResponseMessage response =
                    await server.HttpClient.PostAsync(Constants.RoutePrefix + "/" + Constants.QueryRoute + "/unknown",
                        new StringContent(body, Encoding.UTF8, "application/json"));

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
                response.ReasonPhrase.Should().Match("*'unknown' is not a recognized query name");
            }
        }

        [Fact]
        public async Task When_the_body_is_not_valid_json_it_should_return_the_correct_response()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (var server = TestServer.Create(app => app.UseQueryHost(settings)))
            {
                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                HttpResponseMessage response =
                    await server.HttpClient.PostAsync(Constants.RoutePrefix + "/" + Constants.QueryRoute + "/example",
                        new StringContent("----------------", Encoding.UTF8, "application/json"));

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                response.ReasonPhrase.Should().Match("*body*JSON*");
            }
        }

        [Fact]
        public async Task When_the_body_cannot_be_deserialized_to_a_query_object_it_should_return_the_correct_response()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (var server = TestServer.Create(app => app.UseQueryHost(settings)))
            {
                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                string body = JsonConvert.SerializeObject(new MismatchedQuery
                {
                    OtherProperty = "SomeValue"
                });

                HttpResponseMessage response =
                    await server.HttpClient.PostAsync(Constants.RoutePrefix + "/" + Constants.QueryRoute + "/example",
                        new StringContent(body, Encoding.UTF8, "application/json"));

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                response.ReasonPhrase.Should().Match("*OtherProperty*ExampleQuery*");
            }
        }

        [Fact]
        public async Task When_no_query_handler_can_be_found_it_should_return_the_correct_response()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (var server = TestServer.Create(app => app.UseQueryHost(settings)))
            {
                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                string body = JsonConvert.SerializeObject(new MissingHandlerQuery());

                HttpResponseMessage response =
                    await server.HttpClient.PostAsync(Constants.RoutePrefix + "/" + Constants.QueryRoute + "/missinghandler",
                        new StringContent(body, Encoding.UTF8, "application/json"));

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
                response.ReasonPhrase.Should().Match("Don't know how to handle query*MissingHandlerQuery*");
            }
        }

        [Fact]
        public async Task When_the_result_connect_be_serialized_it_should_return_the_correct_response()
        {
            //-----------------------------------------------------------------------------------------------------------
            // Arrange
            //-----------------------------------------------------------------------------------------------------------
            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof(ExampleQueryHandler).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver());

            using (var server = TestServer.Create(app => app.UseQueryHost(settings)))
            {
                //-----------------------------------------------------------------------------------------------------------
                // Act
                //-----------------------------------------------------------------------------------------------------------
                string body = JsonConvert.SerializeObject(new UnserializableResultQuery());

                HttpResponseMessage response =
                    await server.HttpClient.PostAsync(Constants.RoutePrefix + "/" + Constants.QueryRoute + "/unserializableresult",
                        new StringContent(body, Encoding.UTF8, "application/json"));

                //-----------------------------------------------------------------------------------------------------------
                // Assert
                //-----------------------------------------------------------------------------------------------------------
                response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            }
        }
    }
}