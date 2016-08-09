using System.Threading;

namespace FluidCaching
{
    public class CacheStats
    {
        private int current;
        private int totalCount;
        private long misses;
        private long hits;

        internal CacheStats(int capacity)
        {
            Capacity = capacity;
        }

        public int Capacity { get; set; }

        /// <summary>
        /// The current number of items in the cache.
        /// </summary>
        public int Current => current;

        /// <summary>
        /// Number of items added to the cache since it was created.
        /// </summary>
        public int SinceCreation => totalCount;

        public long Misses => misses;

        public long Hits => hits;

        public void Reset()
        {
            totalCount = 0;
            misses = 0;
            hits = 0;
            current = 0;
        }

        internal void RegisterItem()
        {
            Interlocked.Increment(ref totalCount);
            Interlocked.Increment(ref current);
        }

        internal void UnregisterItem()
        {
            Interlocked.Decrement(ref current);
        }

        internal void RegisterMiss()
        {
            Interlocked.Increment(ref misses);
        }

        internal void RegisterHit()
        {
            Interlocked.Increment(ref hits);
        }

        internal bool RequiresRebuild => totalCount - current > Capacity;

        internal void MarkAsRebuild(int rebuildIndexSize)
        {
            totalCount = rebuildIndexSize;
            current = rebuildIndexSize;
        }

        public override string ToString()
        {
            return $"Capacity: {Capacity}, Current: {current}, Total: {totalCount}, Hits: {hits}, Misses: {misses}";
        }
    }
}