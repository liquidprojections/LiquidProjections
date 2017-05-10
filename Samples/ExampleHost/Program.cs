using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Http;
using System.Web.Http.Dispatcher;
using Microsoft.Owin.Hosting;
using Owin;
using TinyIoC;

namespace LiquidProjections.ExampleHost
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var container = TinyIoCContainer.Current;

            var eventStore = new JsonFileEventStore("ExampleEvents.zip", 100);

            var projectionsStore = new InMemoryDatabase();
            container.Register(projectionsStore);

            var dispatcher = new Dispatcher(eventStore.Subscribe);

            var bootstrapper = new CountsProjector(dispatcher, projectionsStore);

            var startOptions = new StartOptions($"http://localhost:9000");
            using (WebApp.Start(startOptions, builder => builder.UseControllers(container)))
            {
                bootstrapper.Start();

                Console.WriteLine($"HTTP endpoint available at http://localhost:9000/api/Statistics/CountsPerState");

                Console.ReadLine();
            }
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