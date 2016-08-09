using System.Threading.Tasks;

namespace FluidCaching
{
    /// <summary>
    /// The public wrapper for a Index
    /// </summary>
    public interface IIndex<TKey, T> where T : class
    {
        /// <summary>
        /// Getter for index
        /// </summary>
        /// <param name="key">key to find (or load if needed)</param>
        /// <param name="createItem">
        /// An optional delegate that is used to create the actual object if it doesn't exist in the cache.
        /// </param>
        /// <returns>the object value associated with the cache</returns>
        Task<T> GetItem(TKey key, ItemCreator<TKey, T> createItem = null);

        /// <summary>Delete object that matches key from cache</summary>
        /// <param name="key">key to find</param>
        void Remove(TKey key);

        long Count { get; }
    }
}