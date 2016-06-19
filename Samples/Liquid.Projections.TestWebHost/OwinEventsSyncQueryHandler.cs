using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using eVision.QueryHost.Client;
using eVision.QueryHost.Raven;
using eVision.QueryHost.Raven.Querying;

namespace QueryHost.TestWebHost
{
    public class OwinEventsSyncQueryHandler : IQueryHandler<OwinEventsSyncQuery, IList<OwinProjection>>
    {
        private readonly Func<IQueryableRavenSession> sessionFactory;

        public OwinEventsSyncQueryHandler(Func<IQueryableRavenSession> sessionFactory)
        {
            this.sessionFactory = sessionFactory;
        }

        public async Task<IList<OwinProjection>> Handle(OwinEventsSyncQuery query)
        {
            const int pageSize = 128;
            var results = new List<OwinProjection>();
            using (var session = sessionFactory())
            {
                var start = 0;
                var count = pageSize;
                while (count == pageSize)
                {
                    Task<IEnumerable<OwinProjection>> batch = session.Advanced
                        .LoadStartingWithAsync<OwinProjection>(RavenSession.GetId<OwinProjection>(""), null, start, pageSize);

                    IEnumerable<OwinProjection> projections = await batch;

                    results.AddRange(projections);
                    count = projections.Count();
                    start += count;
                }
            }
            return results;
        }
    }
}