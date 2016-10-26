using System;

using FluentNHibernate.Mapping;

namespace LiquidProjections.NHibernate.Specs
{
    public class TrackingState : ITrackingState
    {
        public virtual long Id { get; set; }
        public virtual string ProjectorId { get; set; }
        public virtual long Checkpoint { get; set; }
        public virtual DateTime LastUpdateUtc { get; set; }
    }

    internal sealed class TrackingStateClassMap : ClassMap<TrackingState>
    {
        public TrackingStateClassMap()
        {
            Id(projectorState => projectorState.Id).GeneratedBy.Identity();
            Map(projectorState => projectorState.ProjectorId).Not.Nullable().Length(150).Index("IX_TrackingState_ProjectorId");
            Map(projectorState => projectorState.Checkpoint);
            Map(projectorState => projectorState.LastUpdateUtc);
        }
    }
}