using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using eVision.QueryHost.Client;
using eVision.QueryHost.Dispatching;

using Microsoft.Owin;
using NEventStore;
using Newtonsoft.Json;
using Owin;

namespace QueryHost.TestWebHost
{
    using AppFunc = Func<IDictionary<string, object>, Task>;
    using MidFunc = Func<Func<IDictionary<string, object>, Task>, Func<IDictionary<string, object>, Task>>;

    public static class TestCommandAppBuilderExtensions
    {
        public static IAppBuilder UseTestCommand(this IAppBuilder appBuilder, IStoreEvents eventStore, DurableCommitDispatcher synchronousDispatcher, IQueryProcessor queryProcessor)
        {
            var streamId = Guid.NewGuid();

            appBuilder.Map("/test", app =>
            {
                AppFunc appFunc = env => Task.Run(() =>
                {
                    var commitId = Guid.NewGuid();
                    var context = new OwinContext(env);
                    var request = context.Request;

                    PublishEvent(eventStore, streamId, commitId, request);

                    var events = queryProcessor.Execute(new OwinEventsQuery()).Result;
                    WriteQueryResponse(events, context);
                }, new OwinContext(env).Request.CallCancelled);
                MidFunc func = next => appFunc;
                app.Use(func);
            });

            appBuilder.Map("/testsync", app =>
            {
                AppFunc appFunc = env => Task.Run(() =>
                {
                    var commitId = Guid.NewGuid();
                    var context = new OwinContext(env);
                    var request = context.Request;

                    var synchronizationTask = synchronousDispatcher.DispatchedCommits
                        .Where(c => c.Id == commitId)
                        .Take(1)
                        .ToTask(request.CallCancelled);

                    PublishEvent(eventStore, streamId, commitId, request);
                    synchronousDispatcher.PollNow();
                    synchronizationTask.Wait(TimeSpan.FromSeconds(5));

                    var events = queryProcessor.Execute(new OwinEventsSyncQuery()).Result;
                    WriteQueryResponse(events, context);
                }, new OwinContext(env).Request.CallCancelled);
                MidFunc func = next => appFunc;
                app.Use(func);
            });
            return appBuilder;
        }

        private static void WriteQueryResponse(IEnumerable<OwinProjection> events, OwinContext context)
        {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "text/plain";
            var buffer = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(events, Formatting.Indented));
            context.Response.Body.Write(buffer, 0, buffer.Length);
        }

        private static void PublishEvent(IStoreEvents eventStore, Guid streamId, Guid commitId, IOwinRequest request)
        {
            using (IEventStream stream = eventStore.OpenStream(streamId))
            {
                var evt = new OwinEvent
                {
                    RequestBody = StreamToString(request.Body),
                    RequestHeaders = request.Headers,
                    RequestMethod = request.Method,
                    RequestPath = request.Path.ToString(),
                    RequestPathBase = request.PathBase.ToString(),
                    RequestProtocol = request.Protocol,
                    RequestQueryString = request.QueryString.ToString(),
                    RequestScheme = request.Scheme
                };
                stream.Add(new EventMessage {Body = evt});
                stream.CommitChanges(commitId);
            }
        }

        private static string StreamToString(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}