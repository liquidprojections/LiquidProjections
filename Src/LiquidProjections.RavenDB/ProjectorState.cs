using System;

namespace LiquidProjections.RavenDB
{
    internal class ProjectorState
    {
        public string Id { get; set; }

        public long Checkpoint { get; set; }

        public DateTime LastUpdateUtc { get; set; }
    }
}