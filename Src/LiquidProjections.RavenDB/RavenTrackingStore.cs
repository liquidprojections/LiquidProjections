using System;
using System.Threading.Tasks;
using eVision.FlowVision.Infrastructure.Common.Raven.Liquid;
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

        public async Task<string> LoadCheckpoint(string projectorId)
        {
            using (var session = sessionFactory())
            {
                var state = await session.LoadAsync<ProjectorState>("Checkpoint/" + projectorId);

                return (state != null) ? state.Checkpoint : "";
            }
        }

        public async Task SaveCheckpoint(string projectorId, string checkpoint)
        {
            using (var session = sessionFactory())
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

            public string Checkpoint { get; set; }

            public DateTime LastUpdateUtc { get; set; }
        }
    }
}
