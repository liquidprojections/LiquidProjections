using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client;

namespace LiquidProjections.RavenDB
{
    public delegate string GetEventKey(object @event);

    public delegate long GetEventVersion(object @event);

    public delegate Func<TProjection, RavenProjectionContext, Task> GetEventHandler<in TProjection>(object @event);

    public class RavenProjector<TProjection> where TProjection : IHaveKey, new()
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private readonly GetEventKey getEventKey;
        private readonly GetEventVersion getEventVersion;
        private readonly GetEventHandler<TProjection> getEventHandler;

        public RavenProjector(Func<IAsyncDocumentSession> sessionFactory, 
            GetEventKey getEventKey, GetEventVersion getEventVersion, GetEventHandler<TProjection> getEventHandler)
        {
            this.sessionFactory = sessionFactory;
            this.getEventKey = getEventKey;
            this.getEventVersion = getEventVersion;
            this.getEventHandler = getEventHandler;
        }

        public async Task Handle(IEnumerable<Transaction> transactions)
        {
            foreach (Transaction transaction in transactions)
            {
                using (var session = sessionFactory())
                {
                    foreach (EventEnvelope @event in transaction.Events)
                    {
                        // TODO: try get projection from cache
                        // TODO: handle old versions

                        Func<TProjection, RavenProjectionContext, Task> handler = getEventHandler(@event.Body);
                        if (handler != null)
                        {
                            string key = getEventKey(@event.Body);

                            var projection = await session.LoadAsync<TProjection>(key);
                            if (projection == null)
                            {
                                projection = new TProjection
                                {
                                    Key = key
                                };

                                await session.StoreAsync(projection);
                            }

                            await handler(projection, new RavenProjectionContext
                            {
                                Session = session,
                                StreamId = transaction.StreamId,
                                TimeStampUtc = transaction.TimeStampUtc,
                                Checkpoint = transaction.Checkpoint
                            });
                        }

                        await session.SaveChangesAsync();
                    }
                }
            }
        }
    }
}