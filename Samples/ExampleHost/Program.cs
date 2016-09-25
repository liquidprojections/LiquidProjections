using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;
using Raven.Client;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Raven.Database.Server;
using TinyIoC;

namespace LiquidProjections.ExampleHost
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var container = TinyIoCContainer.Current;

            var eventStore = new JsonFileEventStore("ExampleEvents.zip", 100);

            EmbeddableDocumentStore store = BuildDocumentStore(".\\", 9001);

            container.Register<Func<IAsyncDocumentSession>>(() => store.OpenAsyncSession());
            var dispatcher = new Dispatcher(eventStore);

            var bootstrapper = new CountsProjectionBootstrapper(dispatcher, store.OpenAsyncSession);
            bootstrapper.Start().Wait();

            var startOptions = new StartOptions($"http://localhost:9000");
            using (WebApp.Start(startOptions, builder => builder.UseControllers(container)))
            {
                Console.WriteLine($"HTTP endpoint available at http://localhost:9000/api/Statistics/CountsPerState");
                Console.WriteLine($"Management Studio available at http://localhost:9001");

                Console.ReadLine();
            }
        }

        private static EmbeddableDocumentStore BuildDocumentStore(string rootDir, int? studioPort)
        {
            var dataDir = Path.Combine(rootDir, "Projections");
            var documentStore = new EmbeddableDocumentStore
            {
                DataDirectory = dataDir,
                DefaultDatabase = "Default",
                Conventions =
                {
                    MaxNumberOfRequestsPerSession = 100,
                    ShouldCacheRequest = (url) => false
                },
                Configuration =
                {
                    DisableInMemoryIndexing = true,
                    DataDirectory = dataDir,
                    CountersDataDirectory = Path.Combine(rootDir, "Counters"),
                    CompiledIndexCacheDirectory = Path.Combine(rootDir, "CompiledIndexCache"),
                    DefaultStorageTypeName = "Esent",
                },
                EnlistInDistributedTransactions = false,
            };

            documentStore.Configuration.Settings.Add("Raven/Esent/CacheSizeMax", "256");
            documentStore.Configuration.Settings.Add("Raven/Esent/MaxVerPages", "32");
            documentStore.Configuration.Settings.Add("Raven/MemoryCacheLimitMegabytes", "512");
            documentStore.Configuration.Settings.Add("Raven/MaxNumberOfItemsToIndexInSingleBatch", "4096");
            documentStore.Configuration.Settings.Add("Raven/MaxNumberOfItemsToPreFetchForIndexing", "4096");
            documentStore.Configuration.Settings.Add("Raven/InitialNumberOfItemsToIndexInSingleBatch", "64");

            if (studioPort.HasValue)
            {
                NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(studioPort.Value);
                documentStore.UseEmbeddedHttpServer = true;
                documentStore.Configuration.Port = studioPort.Value;
            }

            documentStore.Initialize();

            IndexCreation.CreateIndexes(typeof(Program).Assembly, documentStore);

            return documentStore;
        }

        internal static IAppBuilder UseControllers(this IAppBuilder app, TinyIoCContainer container)
        {
            HttpConfiguration configuration = BuildHttpConfiguration(container);
            app.Map("/api", a => a.UseWebApi(configuration));

            return app;
        }

        private static HttpConfiguration BuildHttpConfiguration(TinyIoCContainer container)
        {
            var configuration = new HttpConfiguration
            {
                DependencyResolver = new TinyIocWebApiDependencyResolver(container),
                IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always
            };

            configuration.Services.Replace(typeof(IHttpControllerTypeResolver), new ControllerTypeResolver());
            configuration.MapHttpAttributeRoutes();

            return configuration;
        }

        internal class ControllerTypeResolver : IHttpControllerTypeResolver
        {
            public ICollection<Type> GetControllerTypes(IAssembliesResolver assembliesResolver)
            {
                return new List<Type>
                {
                    typeof(StatisticsController)
                };
            }
        }
    }
}