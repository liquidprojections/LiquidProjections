using System;
using System.Linq;
using System.Threading.Tasks;

namespace eVision.QueryHost.Raven.Dispatching
{
    internal sealed class RavenLookupProjector<TProjection, TLookup> : IRavenLookupProjector<TProjection>
        where TProjection : IIdentity
        where TLookup : RavenLookup, new()
    {
        private readonly IWritableRavenSession session;
        private readonly Func<TProjection, string> lookupKeySelector;

        public RavenLookupProjector(IWritableRavenSession session, Func<TProjection, string> lookupKeySelector)
        {
            this.session = session;
            this.lookupKeySelector = lookupKeySelector;
        }

        public string GetKey(TProjection projection)
        {
            return projection != null ? lookupKeySelector(projection) : null;
        }

        public async Task UpdateLookup(string oldKey, string newKey, string projectionId)
        {
            if (oldKey != newKey)
            {
                if (oldKey != null)
                {
                    await RemoveLookup(oldKey, projectionId);
                }
                if (newKey != null)
                {
                    await AddLookup(newKey, projectionId);
                }
            }
        }

        private async Task AddLookup(string lookupKey, string projectionId)
        {
            var lookup = await session.Load<TLookup>(lookupKey);
            if (lookup == null)
            {
                lookup = new TLookup
                {
                    Id = RavenSession.GetId<TLookup>(lookupKey)
                };
                await session.Store(lookup);
            }
            lookup.ProjectionIds = lookup.ProjectionIds.Concat(new[] { projectionId }).ToArray();
        }

        private async Task RemoveLookup(string lookupKey, string projectionId)
        {
            var lookup = await session.Load<TLookup>(lookupKey);
            if (lookup != null)
            {
                lookup.ProjectionIds = lookup.ProjectionIds.Where(x => x != projectionId).ToArray();
                if (!lookup.ProjectionIds.Any())
                {
                    await session.Delete(lookup);
                }
            }
        }
    }
}