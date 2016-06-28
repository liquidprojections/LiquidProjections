using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace LiquidProjections.NEventStore
{
    internal class LruCache<TKey, TValue>
    {
        private readonly int capacity;

        private readonly ConcurrentDictionary<TKey, Node> nodes = new ConcurrentDictionary<TKey, Node>();
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public LruCache(int capacity)
        {
            Debug.Assert(capacity > 10);
            this.capacity = capacity;
        }

        public void Set(TKey key, TValue value)
        {
            var node = new Node
            {
                Value = value,
                Ticks = stopwatch.ElapsedTicks
            };

            nodes.AddOrUpdate(key, node, (_, __) => node);

            if (nodes.Count > capacity)
            {
                Evict();
            }
        }

        private void Evict()
        {
            int targetCount = capacity * 9 / 10;
            var toRemove = nodes.ToArray().OrderBy(x => Volatile.Read(ref x.Value.Ticks)).Take(capacity - targetCount);

            foreach (var source in toRemove)
            {
                if (nodes.Count <= targetCount)
                {
                    break;
                }

                Node _;
                nodes.TryRemove(source.Key, out _);
            }
        }

        public bool TryGet(TKey key, out TValue value)
        {
            Node node;

            if (nodes.TryGetValue(key, out node))
            {
                Volatile.Write(ref node.Ticks, stopwatch.ElapsedTicks);
                value = node.Value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        private class Node
        {
            public long Ticks;
            public TValue Value;
        }
    }
}