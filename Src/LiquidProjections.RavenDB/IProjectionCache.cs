using System;
using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    /// <summary>
    /// Defines a write-through cache that the <see cref="RavenProjector{TProjection, TKey}"/> can use to avoid unnecessary loads
    /// of the projection.
    /// Can be reused for multiple projectors.
    /// </summary>
    public interface IProjectionCache<TProjection>
    {
        /// <summary>
        /// Attempts to get the item identified by <paramref name="key"/> in the database from the cache, or creates a new one. 
        /// </summary>
        /// <remarks>
        /// This method must be safe in multi-threaded scenarios. So multiple concurrent requests for the same key must always 
        /// return the same object.
        /// </remarks>
        Task<TProjection> Get(string key, Func<Task<TProjection>> createProjection);

        /// <summary>
        /// Removes the item identified by <paramref name="key"/> id the database from the cache.
        /// </summary>
        /// This method must be safe in multi-threaded scenarios and be idempotent. 
        /// </summary>
        void Remove(string key);
    }
}