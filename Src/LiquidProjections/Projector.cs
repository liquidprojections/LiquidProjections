using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class Projector
    {
        private readonly IEventMap<ProjectionContext> map;

        public Projector(IEventMapBuilder<ProjectionContext> eventMapBuilder)
        {
            map = eventMapBuilder.Build();
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
            foreach (EventEnvelope @event in transaction.Events)
            {
                Func<ProjectionContext, Task> handler = map.GetHandler(@event.Body);

                if (handler != null)
                {
                    await handler(new ProjectionContext
                    {
                        TransactionId = transaction.Id,
                        StreamId = transaction.StreamId,
                        TimeStampUtc = transaction.TimeStampUtc,
                        Checkpoint = transaction.Checkpoint,
                        EventHeaders = @event.Headers,
                        TransactionHeaders = transaction.Headers
                    });
                }
            }
        }
    }
}
