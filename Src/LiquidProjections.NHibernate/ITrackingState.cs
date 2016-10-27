using System;

namespace LiquidProjections.NHibernate
{
    public interface ITrackingState
    {
        string ProjectorId { get; set; }
        long Checkpoint { get; set; }
        DateTime LastUpdateUtc { get; set; }
    }
}