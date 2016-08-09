using System;
using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    public interface IProjectionCache<TProjection> where TProjection : IHaveIdentity
    {
        Task<TProjection> Get(string key, Func<Task<TProjection>> createProjection);
    }
}