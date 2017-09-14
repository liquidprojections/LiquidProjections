using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LiquidProjections.Statistics
{
    /// <summary>
    /// Provides a thread-safe place to store all kinds of run-time information about the progress of a projector. 
    /// </summary>
    public class ProjectionStats
    {
        private readonly Func<DateTime> nowUtc;
        private readonly ConcurrentDictionary<string, ProjectorStats> stats = new ConcurrentDictionary<string, ProjectorStats>();

        public ProjectionStats(Func<DateTime> nowUtc)
        {
            this.nowUtc = nowUtc;
        }

        /// <summary>
        /// Should be called to track the progress of a projector and use that to calculate an ETA.
        /// </summary>
        public void TrackProgress(string projectorId, long checkpoint)
        {
            this[projectorId].TrackProgress(checkpoint, nowUtc());
        }

        /// <summary>
        /// Can be used to store projector-specific properties that characterize the projector's configuration or state. 
        /// </summary>
        /// <remarks>
        /// Each property is identified by a <paramref name="name"/>. This class only keeps the latest value
        /// for each property.
        /// </remarks>
        public void StoreProperty(string projectorId, string name, string value)
        {
            this[projectorId].StoreProperty(name, value, nowUtc());
        }

        /// <summary>
        /// Can be used to store information that happened that can help diagnose the state or failure of a projector.
        /// </summary>
        public void LogEvent(string projectorId, string body)
        {
            this[projectorId].LogEvent(body, nowUtc());
        }

        /// <summary>
        /// Gets the speed in transactions per minute based on a weighted average over the last
        /// ten minutes, or <c>null</c> if there is not enough information yet.        
        /// </summary>
        /// <param name="projectorId"></param>
        public float? GetSpeed(string projectorId)
        {
            return this[projectorId].GetSpeed();
        }

        /// <summary>
        /// Calculates the expected time for the projector identified by <paramref name="projectorId"/> to reach a 
        /// certain <paramref name="targetCheckpoint"/> based on a weighted average over the last 
        /// ten minutes, or <c>null</c> if there is not enough information yet. Use <see cref="TrackProgress"/> to report
        /// progress.
        /// </summary>
        public TimeSpan? GetTimeToReach(string projectorId, long targetCheckpoint)
        {
            return this[projectorId].GetTimeToReach(targetCheckpoint);
        }

        /// <summary>
        /// Gets the statistics for a particular projector.
        /// </summary>
        public ProjectorStats this[string projectorId]
        {
            get
            {
                return stats.GetOrAdd(projectorId, id => new ProjectorStats(id, nowUtc));
            }
        }

        public IEnumerable<ProjectorStats> GetForAllProjectors()
        {
            return stats.ToArray().Select(projectorStatsById => projectorStatsById.Value);
        }
    }
}
