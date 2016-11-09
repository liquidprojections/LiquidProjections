using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class Projector
    {
        private readonly IEventMap<ProjectionContext> map;
        private readonly IReadOnlyList<Projector> children;

        public Projector(IEventMapBuilder<ProjectionContext> eventMapBuilder, IEnumerable<Projector> children = null)
        {
            if (eventMapBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMapBuilder));
            }

            SetupHandlers(eventMapBuilder);
            map = eventMapBuilder.Build();
            this.children = children?.ToList() ?? new List<Projector>();

            if (this.children.Contains(null))
            {
                throw new ArgumentException("There is null child projector.", nameof(children));
            }
        }

        private void SetupHandlers(IEventMapBuilder<ProjectionContext> eventMapBuilder)
        {
            eventMapBuilder.HandleCustomActionsAs((context, projector) => projector());
        }

        /// <summary>
        /// Instructs the projector to handle a collection of ordered transactions.
        /// </summary>
        /// <param name="transactions">
        /// </param>
        public async Task Handle(IReadOnlyList<Transaction> transactions)
        {
            foreach (Transaction transaction in transactions)
            {
                await ProjectTransaction(transaction);
            }
        }

        private async Task ProjectTransaction(Transaction transaction)
        {
            foreach (EventEnvelope eventEnvelope in transaction.Events)
            {
                await ProjectEvent(
                    eventEnvelope.Body,
                    new ProjectionContext
                    {
                        TransactionId = transaction.Id,
                        StreamId = transaction.StreamId,
                        TimeStampUtc = transaction.TimeStampUtc,
                        Checkpoint = transaction.Checkpoint,
                        EventHeaders = eventEnvelope.Headers,
                        TransactionHeaders = transaction.Headers
                    });
            }
        }

        private async Task ProjectEvent(object anEvent, ProjectionContext context)
        {
            foreach (Projector child in children)
            {
                await child.ProjectEvent(anEvent, context);
            }

            await map.Handle(anEvent, context);
        }
    }
}
