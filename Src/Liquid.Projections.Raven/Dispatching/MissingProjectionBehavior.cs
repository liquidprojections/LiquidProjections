namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// Enum for identifying what to do when the projection can't be found when denormalizing an event.
    /// </summary>
    public enum MissingProjectionBehavior
    {
        /// <summary>
        /// Behavior to create projection when does not exist.
        /// </summary>
        Create,

        /// <summary>
        /// Behavior to ignore the projection when not found.
        /// </summary>
        Ignore
    }
}