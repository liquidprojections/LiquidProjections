using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace eVision.QueryHost.Raven.Dispatching
{
    internal class RavenLookupCollection<TProjection> where TProjection : IIdentity
    {
        private readonly IWritableRavenSession session;
        private readonly List<IRavenLookupProjector<TProjection>> lookups = new List<IRavenLookupProjector<TProjection>>();

        public RavenLookupCollection(IWritableRavenSession session)
        {
            this.session = session;
        }

        public  void WithLookup<TLookup>(Func<TProjection, string> lookupKeySelector)
            where TLookup : RavenLookup, new()
        {
            lookups.Add(new RavenLookupProjector<TProjection, TLookup>(session, lookupKeySelector));
        }

        public Func<TProjection, Task> CreateLookupUpdater(string projectionId, TProjection projection)
        {
            IEnumerable<Tuple<IRavenLookupProjector<TProjection>, string>> preUpdateState = lookups
                .Select(x => Tuple.Create(x, x.GetKey(projection)))
                .ToArray();

            return async p =>
            {
                foreach (Tuple<IRavenLookupProjector<TProjection>, string> tuple in preUpdateState)
                {
                    IRavenLookupProjector<TProjection> lookup = tuple.Item1;
                    string oldKey = tuple.Item2;
                    string newKey = lookup.GetKey(p);
                    await lookup.UpdateLookup(oldKey, newKey, projectionId);
                }
            };
        }
    }
}