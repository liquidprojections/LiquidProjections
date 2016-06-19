using System.Threading.Tasks;

namespace eVision.QueryHost.Raven.Dispatching
{
    internal interface IRavenLookupProjector<in TProjection>
        where TProjection : IIdentity
    {
        string GetKey(TProjection projection);

        Task UpdateLookup(string oldKey, string newKey, string projectionId);
    }
}