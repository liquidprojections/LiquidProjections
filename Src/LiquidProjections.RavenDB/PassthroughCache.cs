using System;
using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    internal class PassthroughCache<TProjection> : IProjectionCache<TProjection>
    {
        public Task<TProjection> Get(string key, Func<Task<TProjection>> createProjection)
        {
            return createProjection();
        }

        public void Remove(string key)
        {
            // Do nothing. Nothing is cached anyway.
        }
    }
}