namespace LiquidProjections.NEventStore.Specs
{
    public abstract class Event
    {
        /// <summary>
        /// Gets or sets the version of the aggregate that this event applies to.
        /// </summary>
        public long Version { get; set; }
    }
}