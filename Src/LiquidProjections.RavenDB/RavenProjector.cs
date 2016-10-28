using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Raven.Client;

namespace LiquidProjections.RavenDB
{
    public class RavenProjector<TProjection, TKey>
        where TProjection : class, IHaveIdentity<TKey>, new()
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private readonly int batchSize;
        private readonly IProjectionCache<TProjection> cache;
        private IEventMap<RavenProjectionContext> map;

        public RavenProjector(
            Func<IAsyncDocumentSession> sessionFactory,
            IEventMapBuilder<TProjection, TKey, RavenProjectionContext> eventMapBuilder,
            int batchSize = 1,
            IProjectionCache<TProjection> cache = null)
        {
            this.sessionFactory = sessionFactory;
            this.batchSize = batchSize;
            this.cache = cache ?? new PassthroughCache<TProjection>();

            CollectionName = typeof(TProjection).Name;

            SetupHandlers(eventMapBuilder);
        }

        /// <summary>
        /// The name of the collection under which the projections must be created (default: the name of the type). 
        /// </summary>
        public string CollectionName { get; set; }

        private void SetupHandlers(IEventMapBuilder<TProjection, TKey, RavenProjectionContext> eventMapBuilder)
        {
            eventMapBuilder.HandleUpdatesAs(async (key, context, projector) =>
            {
                string databaseId = BuildDatabaseId(key);

                TProjection projection = await cache.Get(databaseId, async () =>
                {
                    TProjection existingProjection = await context.Session.LoadAsync<TProjection>(databaseId);
                    return existingProjection ?? new TProjection { Id = key };
                });

                await projector(projection, context);

                await context.Session.StoreAsync(projection, databaseId);
            });

            eventMapBuilder.HandleDeletesAs(async (key, context) =>
            {
                string databaseId = BuildDatabaseId(key);

                if (context.Session.Advanced.IsLoaded(databaseId))
                {
                    var projection = await context.Session.LoadAsync<TProjection>(databaseId);
                    context.Session.Delete(projection);
                }
                else
                {
                    await context.Session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteAsync(databaseId, null);
                }

                cache.Remove(databaseId);
            });

            eventMapBuilder.HandleCustomActionsAs((context, projector) => projector(context));

            map = eventMapBuilder.Build();
        }

        private string BuildDatabaseId(TKey key)
        {
            return $"{CollectionName}/{Convert.ToString(key, CultureInfo.InvariantCulture)}";
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

        private async Task StoreLastCheckpoint(IAsyncDocumentSession session, Transaction transaction)
        {
            string checkpointDatabaseId = GetCheckpointDatabaseId();

            var state = await session.LoadAsync<ProjectorState>(checkpointDatabaseId) ?? new ProjectorState
            {
                Id = checkpointDatabaseId
            };

            state.Checkpoint = transaction.Checkpoint;
            state.LastUpdateUtc = DateTime.UtcNow;

            await session.StoreAsync(state);
        }

        private string GetCheckpointDatabaseId()
        {
            return "RavenCheckpoints/" + CollectionName;
        }

        public async Task<long?> GetLastCheckpoint()
        {
            using (var session = sessionFactory())
            {
                var state = await session.LoadAsync<ProjectorState>(GetCheckpointDatabaseId());
                return state?.Checkpoint;
            }
        }
    }
}