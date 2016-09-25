using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Raven.Client;

namespace LiquidProjections.RavenDB
{
    public class RavenProjector<TProjection> where TProjection : class, IHaveIdentity, new()
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private readonly int batchSize;
        private readonly IProjectionCache<TProjection> cache;
        private readonly IEventMap<TProjection, RavenProjectionContext> map;

        public RavenProjector(Func<IAsyncDocumentSession> sessionFactory, IEventMap<TProjection, RavenProjectionContext> map, int batchSize = 1, IProjectionCache<TProjection> cache = null)
        {
            this.sessionFactory = sessionFactory;
            this.map = map;
            this.batchSize = batchSize;
            this.cache = cache ?? new PassthroughCache<TProjection>();

            InitializeMap();
        }

        private void InitializeMap()
        {
            map.ForwardUpdatesTo(async (key, context, projector) =>
            {
                string id = $"{typeof(TProjection).Name}/{key}";

                TProjection projection = await cache.Get(id, async () =>
                {
                    var p = await context.Session.LoadAsync<TProjection>(id);
                    return p ?? new TProjection
                    {
                        Id = id
                    };
                });

                await projector(projection, context);

                await context.Session.StoreAsync(projection);
            });

            map.ForwardDeletesTo(async (key, context) =>
            {
                string id = $"{typeof(TProjection).Name}/{key}";

                if (context.Session.Advanced.IsLoaded(id))
                {
                    var projection = await context.Session.LoadAsync<TProjection>(id);
                    context.Session.Delete(projection);
                }
                else
                {
                    await context.Session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(id, null);
                }

                cache.Remove(id);
            });

            map.ForwardCustomActionsTo((context, projector) => projector(context));
        }

        /// <summary>
        /// Instructs the projector to project a collection of ordered transactions in batches as defined in the constructor.
        /// </summary>
        /// <param name="transactions">
        /// </param>
        public async Task Handle(IEnumerable<Transaction> transactions)
        {
            foreach (IList<Transaction> batch in transactions.InBatchesOf(batchSize))
            {
                using (IAsyncDocumentSession session = sessionFactory())
                {
                    foreach (Transaction transaction in batch)
                    {
                        await ProjectTransaction(transaction, session);

                        await StoreLastCheckpoint(session, transaction);
                    }

                    await session.SaveChangesAsync();
                }
            }
        }

        private async Task ProjectTransaction(Transaction transaction, IAsyncDocumentSession session)
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
        }

        private static async Task StoreLastCheckpoint(IAsyncDocumentSession session, Transaction transaction)
        {
            string key = "RavenCheckpoints/" + typeof(TProjection).Name;

            var state = await session.LoadAsync<ProjectorState>(key) ?? new ProjectorState
            {
                Id = key
            };

            state.Checkpoint = transaction.Checkpoint;
            state.LastUpdateUtc = DateTime.UtcNow;

            await session.StoreAsync(state);
        }

        public async Task<long?> GetLastCheckpoint()
        {
            using (var session = sessionFactory())
            {
                var state = await session.LoadAsync<ProjectorState>("RavenCheckpoints/" + typeof(TProjection).Name);
                return state?.Checkpoint;
            }
        }
    }

    internal class ProjectorState
    {
        public string Id { get; set; }

        public long Checkpoint { get; set; }

        public DateTime LastUpdateUtc { get; set; }
    }
}