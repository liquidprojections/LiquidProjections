using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace FluidCaching
{
    /// <summary>
    /// Index provides dictionary key / value access to any object in the cache.
    /// </summary>
    internal class Index<TKey, T> : IIndex<TKey, T>, IIndexManagement<T> where T : class
    {
        private readonly FluidCache<T> owner;
        private readonly LifespanManager<T> lifespanManager;
        private ConcurrentDictionary<TKey, WeakReference<INode<T>>> index;
        private readonly GetKey<T, TKey> _getKey;
        private readonly ItemCreator<TKey, T> loadItem;
        

        /// <summary>constructor</summary>
        /// <param name="owner">parent of index</param>
        /// <param name="lifespanManager"></param>
        /// <param name="getKey">delegate to get key from object</param>
        /// <param name="loadItem">delegate to load object if it is not found in index</param>
        public Index(FluidCache<T> owner, LifespanManager<T> lifespanManager, GetKey<T, TKey> getKey, ItemCreator<TKey, T> loadItem)
        {
            Debug.Assert(owner != null, "owner argument required");
            Debug.Assert(getKey != null, "GetKey delegate required");
            this.owner = owner;
            this.lifespanManager = lifespanManager;
            index = new ConcurrentDictionary<TKey, WeakReference<INode<T>>>();
            _getKey = getKey;
            this.loadItem = loadItem;
            RebuildIndex();
        }

        public long Count => index.Count;

        /// <summary>Getter for index</summary>
        /// <param name="key">key to find (or load if needed)</param>
        /// <returns>the object value associated with key, or null if not found & could not be loaded</returns>
        public async Task<T> GetItem(TKey key, ItemCreator<TKey, T> createItem = null)
        {
            INode<T> node = FindExistingNodeByKey(key);
            node?.Touch();

            lifespanManager.CheckValidity();

            ItemCreator<TKey, T> creator = createItem ?? this.loadItem;

            if ((node?.Value == null) && (creator != null))
            {
                node = owner.AddAsNode(await creator(key));
            }

            return node?.Value;
        }

        /// <summary>Delete object that matches key from cache</summary>
        /// <param name="key"></param>
        public void Remove(TKey key)
        {
            INode<T> node = FindExistingNodeByKey(key);
            node?.Remove();

            lifespanManager.CheckValidity();
        }

        /// <summary>try to find this item in the index and return Node</summary>
        public INode<T> FindItem(T item)
        {
            return FindExistingNodeByKey(_getKey(item));
        }

        private INode<T> FindExistingNodeByKey(TKey key)
        {
            WeakReference<INode<T>> reference;
            INode<T> node;
            if (index.TryGetValue(key, out reference) && reference.TryGetTarget(out node))
            {
                lifespanManager.Statistics.RegisterHit();
                return node;
            }

            return null;
        }

        /// <summary>Remove all items from index</summary>
        public void ClearIndex()
        {
            index.Clear();
        }

        /// <summary>AddAsNode new item to index</summary>
        /// <param name="item">item to add</param>
        /// <returns>was item key previously contained in index</returns>
        public bool AddItem(INode<T> item)
        {
            TKey key = _getKey(item.Value);
            return !index.TryAdd(key, new WeakReference<INode<T>>(item, false));
        }

        /// <summary>removes all items from index and reloads each item (this gets rid of dead nodes)</summary>
        public int RebuildIndex()
        {
            lock (lifespanManager)
            {
                // Create a new ConcurrentDictionary, this way there is no need for locking the index itself
                var keyValues = lifespanManager.Select(item => new KeyValuePair<TKey, WeakReference<INode<T>>>(_getKey(item.Value), new WeakReference<INode<T>>(item)));

                index = new ConcurrentDictionary<TKey, WeakReference<INode<T>>>(keyValues);
                return index.Count;
            }
        }
    }
}