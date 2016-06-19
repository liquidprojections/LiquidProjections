using System.Collections.Generic;
using eVision.QueryHost.Client;

namespace QueryHost.TestWebHost
{
    [ApiName("events")]
    public class OwinEventsQuery : IQuery<IList<OwinProjection>> { }
}