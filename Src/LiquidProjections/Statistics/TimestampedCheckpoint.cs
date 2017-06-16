using System;

namespace LiquidProjections.Statistics
{
    public class TimestampedCheckpoint
    {
        public TimestampedCheckpoint(long checkpoint, DateTime timestampUtc)
        {
            Checkpoint = checkpoint;
            TimestampUtc = timestampUtc;
        }

        public long Checkpoint { get; }

        public DateTime TimestampUtc { get;  }
    }
}