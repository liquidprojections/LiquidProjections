using System;
using System.Threading.Tasks;

using NHibernate;

namespace LiquidProjections.NHibernate
{
    public sealed class NHibernateTrackingStore<TState> : ITrackingStore
        where TState : class, ITrackingState, new()
    {
        private readonly Func<ISession> sessionFactory;

        public NHibernateTrackingStore(Func<ISession> sessionFactory)
        {
            this.sessionFactory = sessionFactory;
        }

        public Task SaveCheckpoint(string projectorId, long checkpoint)
        {
            using (ISession session = sessionFactory())
            {
                session.Save(new TState
                {
                    ProjectorId = projectorId,
                    Checkpoint = checkpoint,
                    LastUpdateUtc = DateTime.UtcNow
                });

                session.Flush();
            }

            return Task.FromResult(false);
        }
    }
}