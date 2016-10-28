using System;
using System.Threading.Tasks;
using FluidCaching;

namespace LiquidProjections.RavenDB
{
    public class LruProjectionCache<TProjection> : IProjectionCache<TProjection>
        where TProjection : class
    {
        private readonly IIndex<string, InnerCacheItem> index;
        private readonly FluidCache<InnerCacheItem> cache;

        public LruProjectionCache(int capacity, TimeSpan minimumRetention, TimeSpan maximumRetention, Func<DateTime> getNow)
        {
            cache = new FluidCache<InnerCacheItem>(capacity, minimumRetention, maximumRetention, () => getNow());
            index = cache.AddIndex("projections", innerCacheItem => innerCacheItem.Key);
        }

        public long Hits => cache.Statistics.Hits;

        public long Misses => cache.Statistics.Misses;

        public long CurrentCount => cache.Statistics.Current;

        public async Task<TProjection> Get(string key, Func<Task<TProjection>> createProjection)
        {
            InnerCacheItem innerCacheItem =
                await index.GetItem(key, async _ => new InnerCacheItem(key, await createProjection()));

            return innerCacheItem.Projection;
        }

        public void Remove(string key)
        {
            index.Remove(key);
        }

        public void Add(string key, TProjection projection)
        {
            cache.Add(new InnerCacheItem(key, projection));
        }

        private sealed class InnerCacheItem
        {
            public InnerCacheItem(string key, TProjection projection)
            {
                Key = key;
                Projection = projection;
            }

            public string Key { get; }
            public TProjection Projection { get; }
        }
    }
}