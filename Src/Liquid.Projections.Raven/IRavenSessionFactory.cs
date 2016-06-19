using System;

namespace eVision.QueryHost.Raven
{
    /// <summary>
    /// Base session implementation.
    /// </summary>
    /// <typeparam name="TSession"></typeparam>
    public interface IRavenSessionFactory<out TSession> : IDisposable
        where TSession : RavenSession
    {
        /// <summary>
        /// Creates a new session.
        /// </summary>
        /// <returns>The newly created session.</returns>
        TSession Create();
    }
}