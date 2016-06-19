namespace eVision.QueryHost.Raven
{
    /// <summary>
    /// Determines how a <see cref="RavenSession"/> is created.
    /// </summary>
    public enum SessionCreationOption
    {
        /// <summary>
        /// If another unit of work already exist on the current thread then no new session is created.
        /// </summary>
        ExistingOrNew,

        /// <summary>
        /// A new unit of work is created, regardless of any other existing session.
        /// </summary>
        CreateNew
    }
}