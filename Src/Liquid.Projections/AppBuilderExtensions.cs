using Owin;

namespace eVision.QueryHost
{
    public static class AppBuilderExtensions
    {
        /// <summary>
        /// Hooks up the query host into an OWIN pipeline using the provided <paramref name="settings"/>.
        /// </summary>
        public static IAppBuilder UseProjections(this IAppBuilder appBuilder, QueryHostSettings settings)
        {
            appBuilder.Map("/" + settings.RoutePrefix, app => app.Use(QueryHostMiddleware.Create(settings)));
            return appBuilder;
        }
    }
}