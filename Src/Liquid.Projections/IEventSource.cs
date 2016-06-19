using System;
using System.Threading.Tasks;

namespace eVision.QueryHost
{
    /// <summary>
    /// Represents a source of events to which the QueryHost can listen to.
    /// </summary>
    public interface IEventSource
    {
        /// <summary>
        /// Registers a subscriber that is interested in all <see cref="Transaction"/>s since a specific <paramref name="fromCheckpoint"/>.
        /// </summary>
        IDisposable Subscribe(string fromCheckpoint, Func<Transaction, Task> onCommit);

        /// <summary>
        /// Forces the event source to poll for new events and to notify all subscribers if applicable. 
        /// </summary>
        void RetrieveNow();

        /// <summary>
        /// Compares two checkpoints and returns a value indicating their relative position compared to eachother. 
        /// </summary>
        int CompareCheckpoints(string checkpoint1, string checkpoint2);
    }
}