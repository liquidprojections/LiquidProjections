using System;
using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    internal class PassthroughCache : IProjectionCache
    {
        public void Add(IHaveIdentity projection)
        {
            if (projection == null)
            {
                throw new ArgumentNullException(nameof(projection));
            }

            // Do nothing.
        }

        public Task<TProjection> Get<TProjection>(string key, Func<Task<TProjection>> createProjection)
            where TProjection : class, IHaveIdentity
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is missing.", nameof(key));
            }

            if (createProjection == null)
            {
                throw new ArgumentNullException(nameof(createProjection));
            }

            return createProjection();
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is missing.", nameof(key));
            }

            // Do nothing.
        }

        public Task<IHaveIdentity> TryGet(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key is missing.", nameof(key));
            }

            return Task.FromResult<IHaveIdentity>(null);
        }
    }
}