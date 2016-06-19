namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// Base class for a simple lookup which only indicates the connection.
    /// </summary>
    public abstract class RavenLookup : IIdentity
    {
        /// <summary>
        /// Builds base lookup.
        /// </summary>
        protected RavenLookup()
        {
            ProjectionIds = new string[0];
        }

        /// <summary>
        /// Collection of <see cref="IIdentity" /> ids, referencing the lookup.
        /// </summary>
        public string[] ProjectionIds { get; set; }

        public string Id { get; set; }
    }
}