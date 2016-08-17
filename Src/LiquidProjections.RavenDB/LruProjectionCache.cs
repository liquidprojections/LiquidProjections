using System;
using System.Threading.Tasks;
using FluidCaching;

namespace LiquidProjections.RavenDB
{
    public class LruProjectionCache<TProjection> : IProjectionCache<TProjection> where TProjection : class, IHaveIdentity
    {
        private readonly IIndex<string, TProjection> index;
        private readonly FluidCache<TProjection> cache;

        public LruProjectionCache(int capacity, TimeSpan minimumRetention, TimeSpan maximumRetention, Func<DateTime> getNow)
        {
            cache = new FluidCache<TProjection>(capacity, minimumRetention, maximumRetention, () => getNow());
            index = cache.AddIndex("projections", projection => projection.Id);
        }

        public long Hits => cache.Statistics.Hits;

        public long Misses => cache.Statistics.Misses;

        public long CurrentCount => cache.Statistics.Current;

        public Task<TProjection> Get(string key, Func<Task<TProjection>> createProjection)
        {
            return index.GetItem(key, _ => createProjection());
        }

        public void Remove(string key)
        {
            index.Remove(key);
        }

        public void Add(TProjection projection)
        {
            cache.Add(projection);
        }
    }
}