using System;
using System.Threading.Tasks;
using Raven.Client;

namespace LiquidProjections.RavenDB
{
    public class RavenTrackingStore : ITrackingStore
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;

        public RavenTrackingStore(Func<IAsyncDocumentSession> sessionFactory)
        {
            this.sessionFactory = sessionFactory;
        }

        public async Task<long?> LoadCheckpoint(string projectorId)
        {
            using (IAsyncDocumentSession session = sessionFactory())
            {
                var state = await session.LoadAsync<ProjectorState>("Checkpoint/" + projectorId);

                return state?.Checkpoint;
            }
        }

        public async Task SaveCheckpoint(string projectorId, long checkpoint)
        {
            using (IAsyncDocumentSession session = sessionFactory())
            {
                await session.StoreAsync(new ProjectorState
                {
                    Checkpoint = checkpoint,
                    LastUpdateUtc = DateTime.UtcNow,
                }, "Checkpoint/" + projectorId);

                await session.SaveChangesAsync();
            }
        }

        internal class ProjectorState
        {
            public string Id { get; set; }

            public long Checkpoint { get; set; }

            public DateTime LastUpdateUtc { get; set; }
        }
    }
}