using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reactive.Disposables;
using System.Threading;
using eVision.QueryHost;
using eVision.QueryHost.Client;
using eVision.QueryHost.NEventStore;
using eVision.QueryHost.Raven.Dispatching;
using eVision.QueryHost.Raven.Querying;
using eVision.QueryHost.Raven.Specs;
using Microsoft.Owin;
using NEventStore;
using Owin;
using QueryHost.Specs.Queries;
using QueryHost.TestWebHost;
using Raven.Client.Embedded;
using Raven.Database.Server;

[assembly: OwinStartup(typeof (Startup))]

namespace QueryHost.TestWebHost
{
    public class Startup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            const int portOfRavenDb = 38080;
            NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(portOfRavenDb);

            var documentStore = new EmbeddableDocumentStore
            {
                RunInMemory = true,
                UseEmbeddedHttpServer = true,
                Configuration =
                {
                    Port = portOfRavenDb
                }
            }.Initialize();

            var indexInitialization = new RavenLazyIndexInitializer()
                .For<OwinProjection>()
                .Add<OwinProjection_Any>()
                .SubscribeTo(documentStore);

            IStoreEvents eventStore = Wireup.Init().UsingInMemoryPersistence().Build();

            var factory = new TestRavenSessionFactory(documentStore);

            var dispatcher = new RavenDurableCommitDispatcherBuilder()
                .Named(GetType().Namespace)
                .ListeningTo(new EventStoreClient(eventStore.Advanced))
                .ResolvesSession(factory.Create)
                .WithProjector(session => new OwinEventsProjector(session))
                .Build();

            var disposable = new CompositeDisposable(dispatcher, eventStore, documentStore, indexInitialization);

            dispatcher.Start();

            var settings = new QueryHostSettings()
                .SupportingQueriesFrom(typeof (ExampleQueryHandler).Assembly)
                .SupportingQueriesFrom(typeof (Startup).Assembly)
                .ResolvingQueryHandlersUsing(new TestQueryHandlerResolver(new Dictionary<Type, Func<object>>
                {
                    {typeof (OwinEventsQuery), () => new OwinEventsQueryHandler(factory.Create)},
                    {typeof (OwinEventsSyncQuery), () => new OwinEventsSyncQueryHandler(factory.Create)}
                }));

            appBuilder
                .UseTestCommand(eventStore, dispatcher, new HttpQueryProcessor(new OwinHttpMessageHandler(settings.ToAppFunc())))
                .UseQueryHost(settings);

            // setup projections
            var context = new OwinContext(appBuilder.Properties);
            var token = context.Get<CancellationToken>("host.OnAppDisposing");
            if (token != CancellationToken.None)
            {
                token.Register(disposable.Dispose);
            }
        }
    }
}