namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// Represents a document storable in RavenDB which has an identity.
    /// </summary>
    public interface IIdentity
    {
        /// <summary>
        /// Internal identity of the projection.
        /// </summary>
        string Id { get; set; }
    }
}