using System;
using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    internal class PassthroughCache<TProjection> : IProjectionCache<TProjection> where TProjection : IHaveIdentity
    {
        public Task<TProjection> Get(string key, Func<Task<TProjection>> createProjection)
        {
            return createProjection();
        }
    }
}