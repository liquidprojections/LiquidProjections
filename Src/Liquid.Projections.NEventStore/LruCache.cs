using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace eVision.QueryHost.NEventStore
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
                Ticks = new Reference<long> { Value = stopwatch.ElapsedTicks }
            };

            nodes.AddOrUpdate(key, node, (_, __) => node);
            if (nodes.Count > capacity)
            {
                var toRemove = nodes.OrderBy(x => x.Value.Ticks.Value).Take(nodes.Count / 10).ToArray();
                foreach (var source in toRemove)
                {
                    Node _;
                    nodes.TryRemove(source.Key, out _);
                }
            }
        }

        public bool TryGet(TKey key, out TValue value)
        {
            Node node;
            if (nodes.TryGetValue(key, out node))
            {
                node.Ticks = new Reference<long> { Value = stopwatch.ElapsedTicks };
                value = node.Value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        private class Node
        {
            public volatile Reference<long> Ticks;
            public TValue Value;
        }

        private class Reference<T> { public T Value; }
    }
}