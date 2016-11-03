using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NHibernate;

namespace LiquidProjections.NHibernate
{
    public sealed class NHibernateProjector<TProjection, TKey, TState>
        where TProjection : class, IHaveIdentity<TKey>, new()
        where TState : class, IProjectorState, new()
    {
        private readonly Func<ISession> sessionFactory;
        private readonly int batchSize;
        private readonly IEventMap<NHibernateProjectionContext> map;
        private readonly string stateKey;

        public NHibernateProjector(
            Func<ISession> sessionFactory,
            IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> eventMapBuilder,
            int batchSize = 1,
            string stateKey = null)
        {
            this.sessionFactory = sessionFactory;
            this.batchSize = batchSize;
            this.stateKey = stateKey ?? typeof(TProjection).Name;

            SetupHandlers(eventMapBuilder);
            map = eventMapBuilder.Build();
        }

        private void SetupHandlers(IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> eventMapBuilder)
        {
            eventMapBuilder.HandleCreatesAs(async (key, context, projector) =>
            {
                TProjection projection = new TProjection { Id = key };
                await projector(projection, context);
                context.Session.Save(projection);
            });

            eventMapBuilder.HandleUpdatesAs(async (key, context, projector) =>
            {
                TProjection projection = context.Session.Get<TProjection>(key);

                if (projection == null)
                {
                    throw new NHibernateProjectorException(
                        $"Cannot update {typeof(TProjection)} projection with key {key}. The projection does not exist.");
                }

                await projector(projection, context);
            });

            eventMapBuilder.HandleDeletesAs((key, context) =>
            {
                TProjection existingProjection = context.Session.Get<TProjection>(key);

                if (existingProjection != null)
                {
                    context.Session.Delete(existingProjection);
                }

                return Task.FromResult(false);
            });
        }

        /// <summary>
        /// Instructs the projector to project a collection of ordered transactions in batches as defined in the constructor.
        /// </summary>
        /// <param name="transactions">
        /// </param>
        public async Task Handle(IReadOnlyList<Transaction> transactions)
        {
            foreach (IList<Transaction> batch in transactions.InBatchesOf(batchSize))
            {
                using (ISession session = sessionFactory())
                {
                    session.BeginTransaction();

                    foreach (Transaction transaction in batch)
                    {
                        await ProjectTransaction(transaction, session);
                    }

                    StoreLastCheckpoint(session, batch.Last());
                    session.Transaction.Commit();
                }
            }
        }

        private async Task ProjectTransaction(Transaction transaction, ISession session)
        {
            foreach (EventEnvelope @event in transaction.Events)
            {
                Func<NHibernateProjectionContext, Task> handler = map.GetHandler(@event.Body);

                if (handler != null)
                {
                    await handler(new NHibernateProjectionContext
                    {
                        Session = session,
                        StreamId = transaction.StreamId,
                        TimeStampUtc = transaction.TimeStampUtc,
                        Checkpoint = transaction.Checkpoint,
                        EventHeaders = @event.Headers,
                        TransactionHeaders = transaction.Headers
                    });
                }
            }
        }

        private void StoreLastCheckpoint(ISession session, Transaction transaction)
        {
            TState existingState = session.Get<TState>(stateKey);
            TState state = existingState ?? new TState { Id = stateKey };
            state.Checkpoint = transaction.Checkpoint;
            state.LastUpdateUtc = DateTime.UtcNow;

            if (existingState == null)
            {
                session.Save(state);
            }
        }

        public long? GetLastCheckpoint()
        {
            using (var session = sessionFactory())
            {
                return session.Get<TState>(stateKey)?.Checkpoint;
            }
        }
    }
}