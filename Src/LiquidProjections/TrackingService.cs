using System.Threading.Tasks;
using eVision.FlowVision.Infrastructure.Common.Raven.Liquid;

namespace LiquidProjections
{
    public class TrackingService
    {
        private readonly ITrackingStore store;

        public TrackingService(ITrackingStore store)
        {
            this.store = store;
        }

        public async Task<string> GetLastCheckpoint(string projectorId)
        {
            string checkPoint = await store.LoadCheckpoint(projectorId);
            return checkPoint ?? "";
        }

        public Task SaveCheckpoint(string projectorId, string checkpoint)
        {
            return store.SaveCheckpoint(projectorId, checkpoint);
        }
    }
}