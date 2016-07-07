using System.Threading.Tasks;

namespace eVision.FlowVision.Infrastructure.Common.Raven.Liquid
{
    public interface ITrackingStore
    {
        Task<string> LoadCheckpoint(string projectorId);

        Task SaveCheckpoint(string projectorId, string checkpoint);
    }
}