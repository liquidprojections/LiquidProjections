using eVision.FlowVision.Infrastructure.Common.Raven.Liquid;
using Raven.Client;

namespace LiquidProjections.RavenDB
{
    public class RavenProjectionContext : ProjectionContext
    {
        public IAsyncDocumentSession Session { get; set; }
    }
}