using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using eVision.QueryHost.Client;
using eVision.QueryHost.Raven.Querying;

using Raven.Client;

namespace QueryHost.TestWebHost
{
    public class OwinEventsQueryHandler : IQueryHandler<OwinEventsQuery, IList<OwinProjection>>
    {
        private readonly Func<IQueryableRavenSession> sessionFactory;

        public OwinEventsQueryHandler(Func<IQueryableRavenSession> sessionFactory)
        {
            this.sessionFactory = sessionFactory;
        }

        public Task<IList<OwinProjection>> Handle(OwinEventsQuery query)
        {
            using (var session = sessionFactory())
            {
                return session.Query<OwinProjection, OwinProjection_Any>().Take(1024).ToListAsync();
            }
        }
    }
}