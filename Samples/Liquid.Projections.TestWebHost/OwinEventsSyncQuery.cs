using System.Collections.Generic;
using eVision.QueryHost.Client;

namespace QueryHost.TestWebHost
{
    [ApiName("eventssync")]
    public class OwinEventsSyncQuery : IQuery<IList<OwinProjection>> { }
}