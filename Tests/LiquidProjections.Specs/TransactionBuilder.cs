using System;

namespace LiquidProjections.Specs
{
    internal class TransactionBuilder
    {
        private long checkpoint;

        public TransactionBuilder WithCheckpoint(long checkpoint)
        {
            this.checkpoint = checkpoint;
            return this;
        }

        public Transaction Build()
        {
            return new Transaction
            {
                Checkpoint = checkpoint,
                Id = Guid.NewGuid().ToString(),
                StreamId = Guid.NewGuid().ToString(),
                TimeStampUtc = DateTime.UtcNow
            };
        }
    }
}