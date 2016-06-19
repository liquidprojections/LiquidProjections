namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// Declares that a projection contains version. The version is than used by projector to skip some changes on redispatch.
    /// </summary>
    public interface IHaveVersion
    {
        /// <summary>
        /// Version of the projection.
        /// </summary>
        long Version { get; set; }
    }
}