using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Raven.Client;

namespace LiquidProjections.RavenDB
{
    /// <summary>
    /// Projects events to projections of type <typeparamref name="TProjection"/> stored in RavenDB.
    /// Keeps track of its own state stored in RavenDB in RavenCheckpoints collection.
    /// Can also have child projectors of type <see cref="IRavenChildProjector"/> which project events
    /// in the same session just before the parent projector.
    /// Throws <see cref="RavenProjectionException"/> when it detects known errors in the event handlers.
    /// </summary>
    public class RavenProjector<TProjection>
        where TProjection : class, IHaveIdentity, new()
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private int batchSize;
        private readonly RavenEventMapConfigurator<TProjection> mapConfigurator;

        /// <summary>
        /// Creates a new instance of <see cref="RavenProjector{TProjection}"/>.
        /// </summary>
        /// <param name="sessionFactory">The delegate that creates a new <see cref="IAsyncDocumentSession"/>.</param>
        /// <param name="mapBuilder">
        /// The <see cref="IEventMapBuilder{TProjection,TKey,TContext}"/>
        /// with already configured handlers for all the required events
        /// but not yet configured how to handle custom actions, projection creation, updating and deletion.
        /// The <see cref="IEventMap{TContext}"/> will be created from it.
        /// </param>
        /// <param name="children">An optional collection of <see cref="IRavenChildProjector"/> which project events
        /// in the same session just before the parent projector.</param>
        public RavenProjector(
            Func<IAsyncDocumentSession> sessionFactory,
            IEventMapBuilder<TProjection, string, RavenProjectionContext> mapBuilder,
            IEnumerable<IRavenChildProjector> children = null)
        {
            if (sessionFactory == null)
            {
                throw new ArgumentNullException(nameof(sessionFactory));
            }

            if (mapBuilder == null)
            {
                throw new ArgumentNullException(nameof(mapBuilder));
            }

            this.sessionFactory = sessionFactory;
            mapConfigurator = new RavenEventMapConfigurator<TProjection>(mapBuilder, children);
        }

        /// <summary>
        /// How many transactions should be processed together in one session. Defaults to one.
        /// Should be small enough for RavenDB to be able to handle in one session.
        /// </summary>
        public int BatchSize
        {
            get { return batchSize; }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                batchSize = value;
            }
        }

        /// <summary>
        /// The name of the collection in RavenDB that contains the projections.
        /// Defaults to the name of the projection type <typeparamref name="TProjection"/>.
        /// Is also used as the document name of the projector state in RavenCheckpoints collection.
        /// </summary>
        public string CollectionName
        {
            get { return mapConfigurator.CollectionName; }
            set { mapConfigurator.CollectionName = value; }
        }

        /// <summary>
        /// A cache that can be used to avoid loading projections from the database.
        /// </summary>
        public IProjectionCache Cache
        {
            get { return mapConfigurator.Cache; }
            set { mapConfigurator.Cache = value; }
        }

        /// <summary>
        /// Instructs the projector to project a collection of ordered transactions asynchronously
        /// in batches of the configured size <see cref="BatchSize"/>.
        /// </summary>
        public async Task Handle(IEnumerable<Transaction> transactions)
        {
            if (transactions == null)
            {
                throw new ArgumentNullException(nameof(transactions));
            }

            foreach (IList<Transaction> batch in transactions.InBatchesOf(batchSize))
            {
                using (IAsyncDocumentSession session = sessionFactory())
                {
                    foreach (Transaction transaction in batch)
                    {
                        await ProjectTransaction(transaction, session);
                    }

                    await StoreLastCheckpoint(session, batch.Last());
                    await session.SaveChangesAsync();
                }
            }
        }

        private async Task ProjectTransaction(Transaction transaction, IAsyncDocumentSession session)
        {
            foreach (EventEnvelope eventEnvelope in transaction.Events)
            {
                var context = new RavenProjectionContext
                {
                    Session = session,
                    StreamId = transaction.StreamId,
                    TimeStampUtc = transaction.TimeStampUtc,
                    Checkpoint = transaction.Checkpoint,
                    EventHeaders = eventEnvelope.Headers,
                    TransactionHeaders = transaction.Headers
                };

                await mapConfigurator.ProjectEvent(eventEnvelope.Body, context);
            }
        }

        private Task StoreLastCheckpoint(IAsyncDocumentSession session, Transaction transaction)
        {
            return session.StoreAsync(new ProjectorState
            {
                Id = GetCheckpointId(),
                Checkpoint = transaction.Checkpoint,
                LastUpdateUtc = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Asynchronously determines the checkpoint of the last projected transaction.
        /// </summary>
        public async Task<long?> GetLastCheckpoint()
        {
            using (IAsyncDocumentSession session = sessionFactory())
            {
                var state = await session.LoadAsync<ProjectorState>(GetCheckpointId());
                return state?.Checkpoint;
            }
        }

        private string GetCheckpointId() => "RavenCheckpoints/" + CollectionName;
    }
}