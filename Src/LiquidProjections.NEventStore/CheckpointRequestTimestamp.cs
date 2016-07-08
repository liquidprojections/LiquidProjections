using System;

namespace LiquidProjections.NEventStore
{
    internal sealed class CheckpointRequestTimestamp
    {
        public CheckpointRequestTimestamp(long checkpoint, DateTime dateTimeUtc)
        {
            Checkpoint = checkpoint;
            DateTimeUtc = dateTimeUtc;
        }

        public long Checkpoint { get; }
        public DateTime DateTimeUtc { get; }
    }
}