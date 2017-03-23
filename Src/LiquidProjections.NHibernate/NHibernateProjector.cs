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
    /// Throws <see cref="ProjectionException"/> when it detects errors in the event handlers.
    /// </summary>
    public sealed class NHibernateProjector<TProjection, TKey, TState>
        where TProjection : class, IHaveIdentity<TKey>, new()
        where TState : class, IProjectorState, new()
    {
        private readonly Func<ISession> sessionFactory;
        private readonly NHibernateEventMapConfigurator<TProjection, TKey> mapConfigurator;
        private int batchSize = 1;
        private string stateKey = typeof(TProjection).Name;
        private ShouldRetry shouldRetry = (exception, count) => Task.FromResult(false);

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
            this.sessionFactory = sessionFactory;
            mapConfigurator = new NHibernateEventMapConfigurator<TProjection, TKey>(mapBuilder, children);
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
        /// A delegate that will be executed when projecting a batch of transactions fails.
        /// This delegate returns a value that indicates if the action should be retried.
        /// </summary>
        public ShouldRetry ShouldRetry
        {
            get { return shouldRetry; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "Retry policy is missing.");
                }

                shouldRetry = value;
            }
        }

        /// <summary>
        /// Instructs the projector to project a collection of ordered transactions asynchronously
        /// in batches of the configured size <see cref="BatchSize"/>.
        /// </summary>
        public async Task Handle(IReadOnlyList<Transaction> transactions)
        {
            if (transactions == null)
            {
                throw new ArgumentNullException(nameof(transactions));
            }

            foreach (IList<Transaction> batch in transactions.InBatchesOf(BatchSize))
            {
                await ExecuteWithRetry(() => ProjectTransactionBatch(batch)).ConfigureAwait(false);
            }
        }

        private async Task ExecuteWithRetry(Func<Task> action)
        {
            for (int attempt = 1;;attempt++)
            {
                try
                {
                    await action();
                    break;
                }
                catch (ProjectionException exception)
                {
                    if (!await ShouldRetry(exception, attempt))
                    {
                        throw;
                    }
                }
            }
        }

        private async Task ProjectTransactionBatch(IList<Transaction> batch)
        {
            try
            {
                using (ISession session = sessionFactory())
                {
                    session.BeginTransaction();

                    foreach (Transaction transaction in batch)
                    {
                        await ProjectTransaction(transaction, session).ConfigureAwait(false);
                    }

                    StoreLastCheckpoint(session, batch.Last());
                    session.Transaction.Commit();
                }
            }
            catch (ProjectionException projectionException)
            {
                projectionException.Projector = typeof(TProjection).ToString();
                projectionException.SetTransactionBatch(batch);
                throw;
            }
            catch (Exception exception)
            {
                var projectionException = new ProjectionException("Projector failed to project transaction batch.", exception)
                {
                    Projector = typeof(TProjection).ToString()
                };

                projectionException.SetTransactionBatch(batch);
                throw projectionException;
            }
        }

        private async Task ProjectTransaction(Transaction transaction, ISession session)
        {
            foreach (EventEnvelope eventEnvelope in transaction.Events)
            {
                var context = new NHibernateProjectionContext
                {
                    TransactionId = transaction.Id,
                    Session = session,
                    StreamId = transaction.StreamId,
                    TimeStampUtc = transaction.TimeStampUtc,
                    Checkpoint = transaction.Checkpoint,
                    EventHeaders = eventEnvelope.Headers,
                    TransactionHeaders = transaction.Headers
                };

                try
                {
                    await mapConfigurator.ProjectEvent(eventEnvelope.Body, context).ConfigureAwait(false);
                }
                catch (ProjectionException projectionException)
                {
                    projectionException.TransactionId = transaction.Id;
                    projectionException.CurrentEvent = eventEnvelope;
                    throw;
                }
                catch (Exception exception)
                {
                    throw new ProjectionException("Projector failed to project an event.", exception)
                    {
                        TransactionId = transaction.Id,
                        CurrentEvent = eventEnvelope
                    };
                }
            }
        }

        private void StoreLastCheckpoint(ISession session, Transaction transaction)
        {
            try
            {
                TState existingState = session.Get<TState>(StateKey);
                TState state = existingState ?? new TState { Id = StateKey };
                state.Checkpoint = transaction.Checkpoint;
                state.LastUpdateUtc = DateTime.UtcNow;

                if (existingState == null)
                {
                    session.Save(state);
                }
            }
            catch (Exception exception)
            {
                throw new ProjectionException("Projector failed to store last checkpoint.", exception);
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
    }
}