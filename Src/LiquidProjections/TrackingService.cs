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

        public async Task<long?> GetLastCheckpoint(string projectorId)
        {
            return await store.LoadCheckpoint(projectorId);
        }

        public Task SaveCheckpoint(string projectorId, long checkpoint)
        {
            return store.SaveCheckpoint(projectorId, checkpoint);
        }
    }
}