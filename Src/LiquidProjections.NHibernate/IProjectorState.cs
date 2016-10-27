using System;

namespace LiquidProjections.NHibernate
{
    public interface IProjectorState
    {
        string Id { get; set; }
        long Checkpoint { get; set; }
        DateTime LastUpdateUtc { get; set; }
    }
}