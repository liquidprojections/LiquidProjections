using System;
using System.Threading.Tasks;

using eVision.QueryHost.Dispatching;

namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// Internal implementation of the checkpoint repository using RavenDB.
    /// </summary>
    internal class RavenCheckpointStore : ICheckpointStore
    {
        private readonly string id;
        private readonly Func<IWritableRavenSession> sessionFactory;

        public RavenCheckpointStore(string id, Func<IWritableRavenSession> sessionFactory)
        {
            this.sessionFactory = sessionFactory;
            this.id = RavenSession.GetId<DispatcherCheckpoint>(id);
        }

        public async Task<string> Get()
        {
            using (var session = sessionFactory())
            {
                var checkpoint = await session.Load<DispatcherCheckpoint>(id);
                return (checkpoint != null) ? checkpoint.Checkpoint : null;
            }
        }

        public async Task Put(string checkpointToken)
        {
            using (var session = sessionFactory())
            {
                var checkpoint = await session.Load<DispatcherCheckpoint>(id);
                if (checkpoint == null)
                {
                    checkpoint = new DispatcherCheckpoint
                    {
                        Id = RavenSession.GetId<DispatcherCheckpoint>(id),
                        Checkpoint = checkpointToken
                    };

                    await session.Store(checkpoint);
                }
                else
                {
                    checkpoint.Checkpoint = checkpointToken;
                }

                await session.SaveChanges();
            }
        }

        private class DispatcherCheckpoint : IIdentity
        {
            public string Checkpoint { get; set; }
            public string Id { get; set; }
        }
    }
}