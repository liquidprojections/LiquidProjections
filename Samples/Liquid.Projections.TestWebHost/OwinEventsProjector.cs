using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using eVision.QueryHost.Dispatching;
using eVision.QueryHost.Raven;
using eVision.QueryHost.Raven.Dispatching;

namespace QueryHost.TestWebHost
{
    public class OwinEventsProjector : RavenProjector<OwinProjection>,
        IProject<OwinEvent>
    {
        public OwinEventsProjector(IWritableRavenSession session) : base(session) {}

        public Task Handle(OwinEvent @event, ProjectionContext context)
        {
            // yes, it's not idempotent, but fine for the test like this.
            return OnHandle(Guid.NewGuid().ToString("N"), Constants.IgnoredVersion, projection =>
            {
                projection.RequestBody = @event.RequestBody;
                // Don't just throw anything to projection, map to serializable/deserializable type first
                projection.RequestHeaders = new Dictionary<string, string[]>(@event.RequestHeaders);
                projection.RequestMethod = @event.RequestMethod;
                projection.RequestPath = @event.RequestPath;
                projection.RequestPathBase = @event.RequestPathBase;
                projection.RequestProtocol = @event.RequestProtocol;
                projection.RequestQueryString = @event.RequestQueryString;
                projection.RequestScheme = @event.RequestScheme;
            });
        }
    }
}