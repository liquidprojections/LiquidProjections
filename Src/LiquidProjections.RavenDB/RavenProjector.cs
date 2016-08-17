using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client;

namespace LiquidProjections.RavenDB
{
    public class RavenProjector<TProjection> where TProjection : class, IHaveIdentity, new()
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private readonly IProjectionCache<TProjection> cache;
        private readonly ProjectionMap<RavenProjectionContext> map = new ProjectionMap<RavenProjectionContext>();

        public RavenProjector(Func<IAsyncDocumentSession> sessionFactory, IProjectionCache<TProjection> cache = null)
        {
            this.sessionFactory = sessionFactory;
            this.cache = cache ?? new PassthroughCache<TProjection>();
        }

        public async Task Handle(IEnumerable<Transaction> transactions)
        {
            foreach (Transaction transaction in transactions)
            {
                using (IAsyncDocumentSession session = sessionFactory())
                {
                    foreach (EventEnvelope @event in transaction.Events)
                    {
                        Func<RavenProjectionContext, Task> handler = map.GetHandler(@event.Body);
                        if (handler != null)
                        {
                            await handler(new RavenProjectionContext
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

        public Action<TEvent> Map<TEvent>()
        {
            return new Action<TEvent>(this);
        }

        private void Add<TEvent>(Func<TEvent, RavenProjectionContext, Task> action)
        {
            map.Map<TEvent>().As(action);
        }

        public class Action<TEvent>
        {
            private readonly RavenProjector<TProjection> parent;

            public Action(RavenProjector<TProjection> parent)
            {
                this.parent = parent;
            }

            public void AsUpdateOf(Func<TEvent, string> selector, Action<TProjection, TEvent, RavenProjectionContext> projector)
            {
                AsUpdateOf(selector, (p, e, ctx) =>
                {
                    projector(p, e, ctx);
                    return Task.FromResult(0);
                });
            }

            public void AsUpdateOf(Func<TEvent, string> selector, Func<TProjection, TEvent, RavenProjectionContext, Task> projector)
            {
                parent.Add<TEvent>(async (@event, ctx) =>
                {
                    string key = selector(@event);

                    TProjection projection = await parent.cache.Get(key, async () =>
                    {
                        var p = await ctx.Session.LoadAsync<TProjection>(key);
                        return p ?? new TProjection
                        {
                            Id = key
                        };
                    });

                    await projector(projection, @event, ctx);

                    await ctx.Session.StoreAsync(projection);
                });
            }

            // TODO: Ignore events for which no registration exist
            public void AsDeleteOf(Func<TEvent, string> selector)
            {
                parent.Add<TEvent>((@event, ctx) =>
                {
                    string key = selector(@event);

                    ctx.Session.Delete(key);

                    parent.cache.Remove(key);

                    return Task.FromResult(0);
                });
            }
        }
    }
}