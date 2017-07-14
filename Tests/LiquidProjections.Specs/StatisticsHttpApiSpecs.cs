using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Chill;
using FluentAssertions;
using FluentAssertions.Json;
using LiquidProjections.Owin;
using LiquidProjections.Statistics;
using Microsoft.Owin.Builder;
using Newtonsoft.Json.Linq;
using Xunit;

// ReSharper disable ConvertToLambdaExpression

namespace LiquidProjections.Specs
{
    namespace StatisticsHttpApiSpecs
    {
        public class When_no_specific_projector_is_requested : GivenSubject<HttpClient, HttpResponseMessage>
        {
            public When_no_specific_projector_is_requested()
            {
                Given(() =>
                {
                    var nowUtc = 10.July(2017).At(10, 39).AsUtc();
                    var stats = new ProjectionStats(() => nowUtc);
                    stats.TrackProgress("id1", 1000);
                    stats.TrackProgress("id2", 1000);

                    var appBuilder = new AppBuilder();
                    appBuilder.UseLiquidProjections(stats);

                    var httpClient = new HttpClient(new OwinHttpMessageHandler(appBuilder.Build()));
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    WithSubject(ct => httpClient);

                });

                When(async () =>
                {
                    return await Subject.GetAsync("http://localhost/projectionStats/");
                });
            }

            [Fact]
            public async Task Then_it_should_provide_a_list_of_all_projectors()
            {
                var jtoken = JToken.Parse(await Result.Content.ReadAsStringAsync());

                jtoken.Should().Be(JToken.Parse(@"
                    [
                        {
                            ""projectorId"": ""id1"",
                            ""lastCheckpoint"": 1000,
                            ""lastCheckpointUpdatedUtc"": ""2017-07-10T10:39:00Z"",
                            ""url"": ""http://localhost/projectionStats/id1""
                                        },
                        {
                            ""projectorId"": ""id2"",
                            ""lastCheckpoint"": 1000,
                            ""lastCheckpointUpdatedUtc"": ""2017-07-10T10:39:00Z"",
                            ""url"": ""http://localhost/projectionStats/id2""
                        }
                    ]"));
            }
        }
        public class When_a_specific_projector_is_requested : GivenSubject<HttpClient, HttpResponseMessage>
        {
            public When_a_specific_projector_is_requested()
            {
                Given(() =>
                {
                    var nowUtc = 10.July(2017).At(10, 39).AsUtc();
                    var stats = new ProjectionStats(() => nowUtc);

                    stats.TrackProgress("id1", 1000);
                    stats.StoreProperty("id1", "property1", "value1");

                    var appBuilder = new AppBuilder();
                    appBuilder.UseLiquidProjections(stats);

                    var httpClient = new HttpClient(new OwinHttpMessageHandler(appBuilder.Build()));
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    
                    WithSubject(ct => httpClient);
                });

                When(async () =>
                {

                    return await Subject.GetAsync("http://localhost/projectionStats/id1");
                });
            }

            [Fact]
            public async Task Then_it_should_return_the_last_checkpoint_and_properties()
            {
                var jtoken = JToken.Parse(await Result.Content.ReadAsStringAsync());

                jtoken.Should().Be(JToken.Parse(@"
                    {
                        ""projectorId"": ""id1"",
                        ""lastCheckpoint"": 1000,
                        ""lastCheckpointUpdatedUtc"": ""2017-07-10T10:39:00Z"",
                        ""properties"": [{
                            ""key"": ""property1"",
                            ""value"": ""value1"",
                            ""lastUpdatedUtc"": ""2017-07-10T10:39:00Z""
                        }],
                        ""eventsUrl"": ""http://localhost/projectionStats/id1/events""
                    }"));
            }
        }
        public class When_an_unknown_projector_is_requested : GivenSubject<HttpClient, HttpResponseMessage>
        {
            public When_an_unknown_projector_is_requested()
            {
                Given(() =>
                {
                    var nowUtc = 10.July(2017).At(10, 39).AsUtc();
                    var stats = new ProjectionStats(() => nowUtc);

                    var appBuilder = new AppBuilder();
                    appBuilder.UseLiquidProjections(stats);

                    var httpClient = new HttpClient(new OwinHttpMessageHandler(appBuilder.Build()));
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    
                    WithSubject(ct => httpClient);
                });

                When(async () =>
                {
                    return await Subject.GetAsync("http://localhost/projectionStats/unknown");
                });
            }

            [Fact]
            public async Task Then_it_should_return_some_default_information()
            {
                var jtoken = JToken.Parse(await Result.Content.ReadAsStringAsync());

                jtoken.Should().Be(JToken.Parse(@"
                    {
                        ""projectorId"": ""unknown"",
                        ""lastCheckpoint"": 0,
                        ""lastCheckpointUpdatedUtc"": ""2017-07-10T10:39:00Z"",
                        ""properties"": [],
                        ""eventsUrl"": ""http://localhost/projectionStats/unknown/events""
                    }"));
            }
        }
        public class When_the_events_of_a_specific_projector_are_requested : GivenSubject<HttpClient, HttpResponseMessage>
        {
            public When_the_events_of_a_specific_projector_are_requested()
            {
                Given(() =>
                {
                    var nowUtc = 10.July(2017).At(10, 39).AsUtc();
                    var stats = new ProjectionStats(() => nowUtc);
                    stats.LogEvent("id1", "someevent");

                    var appBuilder = new AppBuilder();
                    appBuilder.UseLiquidProjections(stats);

                    var httpClient = new HttpClient(new OwinHttpMessageHandler(appBuilder.Build()));
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    
                    WithSubject(ct => httpClient);
                });

                When(async () =>
                {

                    return await Subject.GetAsync("http://localhost/projectionStats/id1/events");
                });
            }

            [Fact]
            public async Task Then_it_should_return_the_last_checkpoint_and_properties()
            {
                var jtoken = JToken.Parse(await Result.Content.ReadAsStringAsync());

                jtoken.Should().Be(JToken.Parse(@"
                    {
                        ""projectorId"": ""id1"",
                        ""events"": [{
                            ""body"": ""someevent"",
                            ""timestampUtc"": ""2017-07-10T10:39:00Z""
                        }],
                    }"));
            }
        }
        public class When_the_eta_to_a_checkpoint_is_requested : GivenSubject<HttpClient, HttpResponseMessage>
        {
            public When_the_eta_to_a_checkpoint_is_requested()
            {
                Given(() =>
                {
                    var nowUtc = 10.July(2017).At(10, 39).AsUtc();
                    UseThe(new ProjectionStats(() => nowUtc));
                    The<ProjectionStats>().TrackProgress("id1", 10);

                    nowUtc = nowUtc.Add(1.Minutes());
                    The<ProjectionStats>().TrackProgress("id1", 1000);

                    var appBuilder = new AppBuilder();
                    appBuilder.UseLiquidProjections(The<ProjectionStats>());

                    var httpClient = new HttpClient(new OwinHttpMessageHandler(appBuilder.Build()));
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));
                    
                    WithSubject(ct => httpClient);
                });

                When(async () =>
                {
                    return await Subject.GetAsync("http://localhost/projectionStats/id1/eta/2000");
                });
            }

            [Fact]
            public async Task Then_it_should_return_the_last_checkpoint_and_properties()
            {
                TimeSpan eta = The<ProjectionStats>().GetTimeToReach("id1", 2000).Value;

                var jtoken = JToken.Parse(await Result.Content.ReadAsStringAsync());
                JToken element = jtoken.Should().HaveElement("eta").Which;

                element.Should().HaveElement("days").Which.Value<int>().Should().Be(eta.Days);
                element.Should().HaveElement("hours").Which.Value<int>().Should().Be(eta.Hours);
                element.Should().HaveElement("minutes").Which.Value<int>().Should().Be(eta.Minutes);
                element.Should().HaveElement("seconds").Which.Value<int>().Should().Be(eta.Seconds);
                element.Should().HaveElement("milliseconds").Which.Value<int>().Should().Be(eta.Milliseconds);
            }
        }
    }
}