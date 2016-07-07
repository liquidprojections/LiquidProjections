using System;

namespace eVision.FlowVision.Infrastructure.Common.Raven.Liquid
{
    /// <summary>
    /// Provides contextual information about projecting a commit.
    /// </summary>
    public class ProjectionContext
    {
        /// <summary>
        /// Gets the value which uniquely identifies the stream to which the transaction belongs.
        /// </summary>
        public string StreamId { get; set; }

        /// <summary>
        /// Gets or sets the point in time at which the commit that is currently being dispatched was persisted.
        /// </summary>
        public DateTime TimeStampUtc { get; set; }

        /// <summary>
        /// Gets or sets the checkpoint of the commit that is currently being dispatched.
        /// </summary>
        public string CheckPoint { get; set; }
    }
}