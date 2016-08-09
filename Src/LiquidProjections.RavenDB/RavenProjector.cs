using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client;

namespace LiquidProjections.RavenDB
{
    public delegate string GetEventKey(object @event);

    public delegate Func<TProjection, RavenProjectionContext, Task> GetEventHandler<in TProjection>(object @event);

    public class RavenProjector<TProjection> where TProjection : IHaveIdentity, new()
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private readonly GetEventKey getEventKey;
        private readonly GetEventHandler<TProjection> getEventHandler;
        private readonly IProjectionCache<TProjection> cache;

        public RavenProjector(Func<IAsyncDocumentSession> sessionFactory, 
            GetEventKey getEventKey, GetEventHandler<TProjection> getEventHandler, IProjectionCache<TProjection> cache = null)
        {
            this.sessionFactory = sessionFactory;
            this.getEventKey = getEventKey;
            this.getEventHandler = getEventHandler;
            this.cache = cache ?? new PassthroughCache<TProjection>();
        }

        public async Task Handle(IEnumerable<Transaction> transactions)
        {
            foreach (Transaction transaction in transactions)
            {
                using (var session = sessionFactory())
                {
                    foreach (EventEnvelope @event in transaction.Events)
                    {
                        Func<TProjection, RavenProjectionContext, Task> handler = getEventHandler(@event.Body);
                        if (handler != null)
                        {
                            string key = getEventKey(@event.Body);

                            TProjection p = await cache.Get(key, async () => 
                            {
                                var projection = await session.LoadAsync<TProjection>(key);
                                if (projection == null)
                                {
                                    projection = new TProjection
                                    {
                                        Id = key
                                    };

                                }

                                return projection;
                            });

                            await session.StoreAsync(p);

                            await handler(p, new RavenProjectionContext
                            {
                                Session = session,
                                StreamId = transaction.StreamId,
                                TimeStampUtc = transaction.TimeStampUtc,
                                Checkpoint = transaction.Checkpoint
                            });
                        }

                    }

                    await session.SaveChangesAsync();
                }
            }
        }
    }
}