using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NHibernate;

namespace LiquidProjections.NHibernate
{
    /// <summary>
    /// Projects events to projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
    /// stored in a database accessed via NHibernate.
    /// Keeps track of its own state stored in the database as <typeparamref name="TState"/>.
    /// Can also have child projectors of type <see cref="INHibernateChildProjector"/> which project events
    /// in the same transaction just before the parent projector.
    /// Uses context of type <see cref="NHibernateProjectionContext"/>.
    /// Throws <see cref="NHibernateProjectionException"/> when it detects known errors in the event handlers.
    /// </summary>
    public sealed class NHibernateProjector<TProjection, TKey, TState>
        where TProjection : class, IHaveIdentity<TKey>, new()
        where TState : class, IProjectorState, new()
    {
        private readonly NHibernateProjector<TState> innerProjector;

        /// <summary>
        /// Creates a new instance of <see cref="NHibernateProjector{TProjection,TKey,TState}"/>.
        /// </summary>
        /// <param name="sessionFactory">The delegate that creates a new <see cref="ISession"/>.</param>
        /// <param name="mapBuilder">
        /// The <see cref="IEventMapBuilder{TProjection,TKey,TContext}"/>
        /// with already configured handlers for all the required events
        /// but not yet configured how to handle custom actions, projection creation, updating and deletion.
        /// The <see cref="IEventMap{TContext}"/> will be created from it.
        /// </param>
        /// <param name="children">An optional collection of <see cref="INHibernateChildProjector"/> which project events
        /// in the same transaction just before the parent projector.</param>
        public NHibernateProjector(
            Func<ISession> sessionFactory,
            IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> mapBuilder,
            IEnumerable<INHibernateChildProjector> children = null)
        {
            innerProjector = new NHibernateProjector<TState>(sessionFactory, new NHibernateEventMapConfigurator<TProjection, TKey>(mapBuilder, children));
        }

        /// <summary>
        /// How many transactions should be processed together in one database transaction. Defaults to one.
        /// </summary>
        public int BatchSize
        {
            get { return innerProjector.BatchSize; }
            set { innerProjector.BatchSize = value; }
        }

        /// <summary>
        /// The key to store the projector state as <typeparamref name="TState"/>.
        /// </summary>
        public string StateKey
        {
            get { return innerProjector.StateKey; }
            set { innerProjector.StateKey = value; }
        }

        /// <summary>
        /// Instructs the projector to project a collection of ordered transactions asynchronously
        /// in batches of the configured size <see cref="BatchSize"/>.
        /// </summary>
        public Task Handle(IReadOnlyList<Transaction> transactions)
        {
            return innerProjector.Handle(transactions);
        }

        /// <summary>
        /// Determines the checkpoint of the last projected transaction.
        /// </summary>
        public long? GetLastCheckpoint()
        {
            return innerProjector.GetLastCheckpoint();
        }
    }


    /// <summary>
    /// Projects events using a custom mapping.
    /// Keeps track of its own state stored in the database as <typeparamref name="TState"/>.
    /// Uses context of type <see cref="NHibernateProjectionContext"/>.
    /// Throws <see cref="NHibernateProjectionException"/> when it detects known errors in the event handlers.
    /// </summary>
    public sealed class NHibernateProjector<TState>
        where TState : class, IProjectorState, new()
    {
        private readonly Func<ISession> sessionFactory;
        private readonly IEventMap<NHibernateProjectionContext> eventMap;
        private int batchSize = 1;
        private string stateKey = string.Empty;

        /// <summary>
        /// Creates a new instance of <see cref="NHibernateProjector{TState}"/>.
        /// </summary>
        /// <param name="sessionFactory">The delegate that creates a new <see cref="ISession"/>.</param>
        public NHibernateProjector(Func<ISession> sessionFactory, IEventMap<NHibernateProjectionContext> eventMap)
        {
            this.sessionFactory = sessionFactory;
            this.eventMap = eventMap;
        }

        /// <summary>
        /// How many transactions should be processed together in one database transaction. Defaults to one.
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
        /// The key to store the projector state as <typeparamref name="TState"/>.
        /// </summary>
        public string StateKey
        {
            get { return stateKey; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("State key is missing.", nameof(value));
                }

                stateKey = value;
            }
        }

        /// <summary>
        /// Instructs the projector to project a collection of ordered transactions asynchronously
        /// in batches of the configured size <see cref="BatchSize"/>.
        /// </summary>
        public async Task Handle(IReadOnlyList<Transaction> transactions)
        {
            foreach (IList<Transaction> batch in transactions.InBatchesOf(BatchSize))
            {
                using (ISession session = sessionFactory())
                {
                    session.BeginTransaction();

                    foreach (Transaction transaction in batch)
                    {
                        await ProjectTransaction(transaction, session);
                    }

                    StoreLastCheckpoint(session, batch.Last().Checkpoint);
                    session.Transaction.Commit();
                }
            }
        }

        private async Task ProjectTransaction(Transaction transaction, ISession session)
        {
            foreach (EventEnvelope eventEnvelope in transaction.Events)
            {
                var context = new NHibernateProjectionContext
                {
                    Session = session,
                    StreamId = transaction.StreamId,
                    TimeStampUtc = transaction.TimeStampUtc,
                    Checkpoint = transaction.Checkpoint,
                    EventHeaders = eventEnvelope.Headers,
                    TransactionHeaders = transaction.Headers
                };

                await eventMap.Handle(eventEnvelope.Body, context);
            }
        }

        /// <summary>
        /// Determines the checkpoint of the last projected transaction.
        /// </summary>
        public long? GetLastCheckpoint()
        {
            using (var session = sessionFactory())
            {
                return session.Get<TState>(StateKey)?.Checkpoint;
            }
        }

        private void StoreLastCheckpoint(ISession session, long checkpoint)
        {
            TState existingState = session.Get<TState>(StateKey);
            TState state = existingState ?? new TState { Id = StateKey };
            state.Checkpoint = checkpoint;
            state.LastUpdateUtc = DateTime.UtcNow;

            if (existingState == null)
            {
                session.Save(state);
            }
        }
    }
}