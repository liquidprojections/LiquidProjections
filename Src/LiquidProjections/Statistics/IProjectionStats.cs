using System;
using System.Collections.Generic;

namespace LiquidProjections.Statistics
{
    public interface IProjectionStats : IEnumerable<IProjectorStats>
    {
        /// <summary>
        /// Should be called to track the progress of a projector and use that to calculate an ETA.
        /// </summary>
        void TrackProgress(string projectorId, long checkpoint);

        /// <summary>
        /// Can be used to store projector-specific properties that characterize the projector's configuration or state. 
        /// </summary>
        /// <remarks>
        /// Each property is identified by a <paramref name="name"/>. This class only keeps the latest value
        /// for each property.
        /// </remarks>
        void StoreProperty(string projectorId, string name, string value);

        /// <summary>
        /// Can be used to store information that happened that can help diagnose the state or failure of a projector.
        /// </summary>
        void LogEvent(string projectorId, string body);

        /// <summary>
        /// Gets the speed in transactions per minute based on a weighted average over the last
        /// ten minutes, or <c>null</c> if there is not enough information yet.        
        /// </summary>
        /// <param name="projectorId"></param>
        float? GetSpeed(string projectorId);

        /// <summary>
        /// Calculates the expected time for the projector identified by <paramref name="projectorId"/> to reach a 
        /// certain <paramref name="targetCheckpoint"/> based on a weighted average over the last 
        /// ten minutes, or <c>null</c> if there is not enough information yet. Use <see cref="ProjectionStats.TrackProgress"/> to report
        /// progress.
        /// </summary>
        TimeSpan? GetTimeToReach(string projectorId, long targetCheckpoint);

        /// <summary>
        /// Gets the stats for an individual projector.
        /// </summary>
        IProjectorStats Get(string projectorId);
    }
}