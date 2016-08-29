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
        private readonly EventMap<RavenProjectionContext> map = new EventMap<RavenProjectionContext>();

        public RavenProjector(Func<IAsyncDocumentSession> sessionFactory, IProjectionCache<TProjection> cache = null)
        {
            this.sessionFactory = sessionFactory;
            this.cache = cache ?? new PassthroughCache<TProjection>();
        }

        public async Task Handle(IEnumerable<Transaction> transactions)
        {
            foreach (Transaction transaction in transactions)
            {
                IAsyncDocumentSession session = null;

                foreach (EventEnvelope @event in transaction.Events)
                {
                    Func<RavenProjectionContext, Task> handler = map.GetHandler(@event.Body);
                    if (handler != null)
                    {
                        if (session == null)
                            session = sessionFactory();

                        await handler(new RavenProjectionContext
                        {
                            Session = session,
                            StreamId = transaction.StreamId,
                            TimeStampUtc = transaction.TimeStampUtc,
                            Checkpoint = transaction.Checkpoint
                        });
                    }
                }

                if (session != null)
                {
                    try
                    {
                        await session.SaveChangesAsync();
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
        }

        public EventAction<TEvent> Map<TEvent>()
        {
            return new EventAction<TEvent>(this);
        }

        private void Add<TEvent>(Func<TEvent, RavenProjectionContext, Task> action)
        {
            map.Map<TEvent>().As(action);
        }

        public class EventAction<TEvent>
        {
            private readonly RavenProjector<TProjection> parent;

            internal EventAction(RavenProjector<TProjection> parent)
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

            public void AsUpdateOf(Func<TEvent, string> selector,
                Func<TProjection, TEvent, RavenProjectionContext, Task> projector)
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

            public void AsDeleteOf(Func<TEvent, string> selector)
            {
                parent.Add<TEvent>(async (@event, ctx) =>
                {
                    string key = selector(@event);

                    if (ctx.Session.Advanced.IsLoaded(key))
                    {
                        ctx.Session.Delete(key);
                    }
                    else
                    {
                        await ctx.Session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(key, null);
                    }

                    parent.cache.Remove(key);
                });
            }

            public void As(Func<TEvent, RavenProjectionContext, Task> action)
            {
                parent.Add(action);
            }
        }
    }
}