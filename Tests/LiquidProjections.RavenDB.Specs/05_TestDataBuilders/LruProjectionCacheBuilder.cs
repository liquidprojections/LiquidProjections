using System;
using System.Collections.Generic;
using System.Linq;

namespace LiquidProjections.RavenDB.Specs._05_TestDataBuilders
{
    internal class LruProjectionCacheBuilder
    {
        private int capacity = 1000;
        private TimeSpan minimumRetention = TimeSpan.FromMinutes(1);
        private TimeSpan maximumRetention = TimeSpan.FromHours(1);
        private readonly List<object> documents = new List<object>();

        public LruProjectionCacheBuilder WithCapacity(int capacity)
        {
            this.capacity = capacity;
            return this;
        }

        public LruProjectionCacheBuilder WithMinimumRetention(TimeSpan timeSpan)
        {
            minimumRetention = timeSpan;
            return this;
        }
        public LruProjectionCacheBuilder WithMaximumRetention(TimeSpan timeSpan)
        {
            maximumRetention = timeSpan;
            return this;
        }

        public LruProjectionCacheBuilder Containing(object document)
        {
            documents.Add(document);
            return this;
        }

        public LruProjectionCache<TProjection> Build<TProjection>() where TProjection : class, IHaveIdentity
        {
            var cache = new LruProjectionCache<TProjection>(capacity, minimumRetention, maximumRetention, () => DateTime.Now);
            foreach (TProjection document in documents.OfType<TProjection>())
            {
                cache.Add(document);
            }

            return cache;
        }
    }
}
