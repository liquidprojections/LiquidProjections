using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace LiquidProjections.Statistics
{
    public interface IProjectorStats
    {
        string ProjectorId { get; }
        TimestampedCheckpoint LastCheckpoint { get; }

        /// <summary>
        /// Gets a snapshot of the properties stored for this projector at the time of calling.
        /// </summary>
        IDictionary<string, Property> GetProperties();

        /// <summary>
        /// Gets a snapshot of the events stored for this projector at the time of calling.
        /// </summary>
        IReadOnlyList<Event> GetEvents();
    }

    /// <summary>
    /// Contains statistics and information about a particular projector.
    /// </summary>
    /// <remarks>
    /// An instance of this class is safe for use in multi-threaded solutions.
    /// </remarks>
    public class ProjectorStats : IProjectorStats
    {
        private readonly object eventsSyncObject = new object();
        private readonly object progressSyncObject = new object();
        private readonly ConcurrentDictionary<string, Property> properties;
        private readonly List<Event> events;

        private readonly WeightedProjectionSpeedCalculator lastMinuteSamples =
            new WeightedProjectionSpeedCalculator(12, TimeSpan.FromSeconds(5));

        private readonly WeightedProjectionSpeedCalculator last10MinuteSamples
            = new WeightedProjectionSpeedCalculator(9, TimeSpan.FromMinutes(1));

        private TimestampedCheckpoint lastCheckpoint;

        public ProjectorStats(string projectorId, Func<DateTime> nowUtc)
        {
            properties = new ConcurrentDictionary<string, Property>();
            events = new List<Event>();
            ProjectorId = projectorId;
            lastCheckpoint = new TimestampedCheckpoint(0, nowUtc());
        }

        public ProjectorStats(string projectorId, IDictionary<string, Property> properties, IEnumerable<Event> events)
        {
            this.properties = new ConcurrentDictionary<string, Property>(properties);
            this.events = this.events.ToList();
            ProjectorId = projectorId;
        }

        public string ProjectorId { get; }

        public TimestampedCheckpoint LastCheckpoint
        {
            get
            {
                lock (progressSyncObject)
                {
                    return lastCheckpoint;
                }
            }
        }

        /// <summary>
        /// Calculates the speed in transactions per second based on a weighted average over the last 10 minutes 
        /// or <c>null</c> if no information is yet available.
        /// </summary>
        public float? GetSpeed()
        {
            float? speed = lastMinuteSamples.GetWeightedSpeed();

            speed = (speed == null)
                ? last10MinuteSamples.GetWeightedSpeed()
                : last10MinuteSamples.GetWeightedSpeedIncluding(speed.Value);

            return speed;
        }

        /// <summary>
        /// Gets a snapshot of the properties stored for this projector at the time of calling.
        /// </summary>
        public IDictionary<string, Property> GetProperties() => properties.ToArray().ToDictionary(p => p.Key, p => p.Value);

        /// <summary>
        /// Gets a snapshot of the events stored for this projector at the time of calling.
        /// </summary>
        public IReadOnlyList<Event> GetEvents()
        {
            lock (eventsSyncObject)
            {
                return events.ToReadOnly();
            }
        }

        public void StoreProperty(string key, string value, DateTime timestampUtc)
        {
            properties[key] = new Property(value, timestampUtc);
        }

        public void LogEvent(string body, DateTime timestampUtc)
        {
            lock (eventsSyncObject)
            {
                events.Add(new Event(body, timestampUtc));
            }
        }

        /// <summary>
        /// Calculates the expected time for a projector to reach a certain <paramref name="targetCheckpoint"/> based 
        /// on a weighted average over the last ten minutes, or <c>null</c> if there is not enough information yet.
        /// </summary>
        public TimeSpan? GetTimeToReach(long targetCheckpoint)
        {
            lock (progressSyncObject)
            {
                if (lastCheckpoint == null)
                {
                    return null;
                }

                if (targetCheckpoint <= lastCheckpoint.Checkpoint)
                {
                    return TimeSpan.Zero;
                }

                float? speed = GetSpeed();
                if (speed == null)
                {
                    return null;
                }
                
                float secondsWithFractionalPart = (targetCheckpoint - lastCheckpoint.Checkpoint) / speed.Value;
                if (secondsWithFractionalPart > long.MaxValue)
                {
                    return null;
                }

                long secondsWithoutFractionalPart = (long) secondsWithFractionalPart;

                return TimeSpan.FromSeconds(secondsWithoutFractionalPart);
            }
        }

        public void TrackProgress(long checkpoint, DateTime timestampUtc)
        {
            lock (progressSyncObject)
            {
                lastMinuteSamples.Record(checkpoint, timestampUtc);
                last10MinuteSamples.Record(checkpoint, timestampUtc);
                lastCheckpoint = new TimestampedCheckpoint(checkpoint, timestampUtc);
            }
        }
    }
}