using System;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class TrackingService
    {
        private readonly ITrackingStore store;

        public TrackingService(ITrackingStore store)
        {
            if (store == null)
            {
                throw new ArgumentNullException(nameof(store));
            }

            this.store = store;
        }

        public Task SaveCheckpoint(string projectorId, long checkpoint)
        {
            return store.SaveCheckpoint(projectorId, checkpoint);
        }
    }
}