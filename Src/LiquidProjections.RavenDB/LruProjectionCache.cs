using System;
using System.Threading.Tasks;
using FluidCaching;

namespace LiquidProjections.RavenDB
{
    public class LruProjectionCache : IProjectionCache
    {
        private readonly IIndex<string, IHaveIdentity> index;
        private readonly FluidCache<IHaveIdentity> cache;

        public LruProjectionCache(int capacity, TimeSpan minimumRetention, TimeSpan maximumRetention, Func<DateTime> getUtcNow)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            if (minimumRetention < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumRetention));
            }

            if (maximumRetention < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRetention));
            }

            if (minimumRetention > maximumRetention)
            {
                throw new ArgumentException("Minimum retention is greater than maximum retention.");
            }

            if (getUtcNow == null)
            {
                throw new ArgumentNullException(nameof(getUtcNow));
            }

            cache = new FluidCache<IHaveIdentity>(capacity, minimumRetention, maximumRetention, () => getUtcNow());
            index = cache.AddIndex("projections", projection => projection.Id);
        }

        public long Hits => cache.Statistics.Hits;

        public long Misses => cache.Statistics.Misses;

        public long CurrentCount => cache.Statistics.Current;

        public async Task<TProjection> Get<TProjection>(string key, Func<Task<TProjection>> createProjection)
            where TProjection : class, IHaveIdentity
        {
            return (TProjection)await index.GetItem(key, async _ => await createProjection());
        }

        public void Remove(string key)
        {
            index.Remove(key);
        }

        public void Add(IHaveIdentity projection)
        {
            cache.Add(projection);
        }

        public Task<IHaveIdentity> TryGet(string key)
        {
            return index.GetItem(key);
        }
    }
}