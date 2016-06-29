using System;

namespace LiquidProjections.NEventStore
{
    internal sealed class CheckpointRequestTimestamp
    {
        public CheckpointRequestTimestamp(string checkpoint, DateTime dateTimeUtc)
        {
            Checkpoint = checkpoint;
            DateTimeUtc = dateTimeUtc;
        }

        public string Checkpoint { get; }
        public DateTime DateTimeUtc { get; }
    }
}