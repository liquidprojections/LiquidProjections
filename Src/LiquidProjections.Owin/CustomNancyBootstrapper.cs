using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LiquidProjections.Statistics;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Configuration;
using Nancy.Extensions;
using Nancy.Linker;
using Nancy.Metadata.Modules;
using Nancy.Routing;
using Nancy.Swagger;
using Nancy.Swagger.Modules;
using Nancy.Swagger.Services;
using Nancy.TinyIoc;

namespace LiquidProjections.Owin
{
    internal class CustomNancyBootstrapper : DefaultNancyBootstrapper
    {
        private readonly IProjectionStats stats;

        public CustomNancyBootstrapper(IProjectionStats stats)
        {
            this.stats = stats;
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            base.ApplicationStartup(container, pipelines);

            SwaggerMetadataProvider.SetInfo(
                "LiquidProjections",
                Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                "Provides statistics about running projectors"
            );
        }

#if DEBUG

        public override void Configure(INancyEnvironment environment)
        {
            environment.Tracing(enabled: false, displayErrorTraces: true);
            base.Configure(environment);
        }
#endif

        protected override IAssemblyCatalog AssemblyCatalog => new StaticAssemblyCatalog();

        protected override ITypeCatalog TypeCatalog => new InternalTypeCatalog(AssemblyCatalog);

        protected override void ConfigureApplicationContainer(TinyIoCContainer container)
        {
            container.Register<IResourceLinker>((x, overloads) =>
                new ResourceLinker(x.Resolve<IRouteCacheProvider>(),
                    x.Resolve<IRouteSegmentExtractor>(), x.Resolve<IUriFilter>()));

            container.Register<IRegistrations, Registration>("LinkerRegistrations");
            container.Register<IRegistrations, SwaggerRegistrations>("SwaggerRegistrations");
            container.Register(stats);
        }

        protected override IEnumerable<ModuleRegistration> Modules =>
            new[]
            {
                new ModuleRegistration(typeof(SwaggerModule)),
                new ModuleRegistration(typeof(StatisticsModule))
            };
    }

    internal class StaticAssemblyCatalog : IAssemblyCatalog
    {
        public IReadOnlyCollection<Assembly> GetAssemblies()
        {
            return new[]
            {
                typeof(MetadataModuleRegistrations).Assembly,
                typeof(CustomNancyBootstrapper).Assembly,
                typeof(DefaultNancyBootstrapper).Assembly
            }.Distinct().ToArray();
        }
    }

    /// <summary>
    /// Implementation of the <see cref="T:Nancy.ITypeCatalog" /> interface that will find internal types as well.
    /// </summary>
    internal class InternalTypeCatalog : ITypeCatalog
    {
        private readonly IAssemblyCatalog assemblyCatalog;
        private readonly ConcurrentDictionary<Type, IReadOnlyCollection<Type>> cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Nancy.DefaultTypeCatalog" /> class.
        /// </summary>
        /// <param name="assemblyCatalog">An <see cref="T:Nancy.IAssemblyCatalog" /> instanced, used to get the assemblies that types should be resolved from.</param>
        public InternalTypeCatalog(IAssemblyCatalog assemblyCatalog)
        {
            this.assemblyCatalog = assemblyCatalog;
            cache = new ConcurrentDictionary<Type, IReadOnlyCollection<Type>>();
        }

        /// <summary>
        /// Gets all types that are assignable to the provided <paramref name="type" />.
        /// </summary>
        /// <param name="type">The <see cref="T:System.Type" /> that returned types should be assignable to.</param>
        /// <param name="strategy">A <see cref="T:Nancy.TypeResolveStrategy" /> that should be used when retrieving types.</param>
        /// <returns>An <see cref="T:System.Collections.Generic.IReadOnlyCollection`1" /> of <see cref="T:System.Type" /> instances.</returns>
        public IReadOnlyCollection<Type> GetTypesAssignableTo(Type type, TypeResolveStrategy strategy)
        {
            return cache.GetOrAdd(type, t => GetTypesAssignableTo(type))
                .Where(strategy.Invoke).ToArray();
        }

        private IReadOnlyCollection<Type> GetTypesAssignableTo(Type type)
        {
            return assemblyCatalog.GetAssemblies()
                .SelectMany(assembly => assembly.SafeGetTypes())
                .Where(type.IsAssignableFrom)
                .Where(t => !t.GetTypeInfo().IsAbstract).ToArray();
        }
    }
}