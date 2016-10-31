using System;
using System.Collections.Generic;

namespace LiquidProjections
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
        public long Checkpoint { get; set; }

        /// <summary>
        /// A collection of named headers related to the event.
        /// </summary>
        public IDictionary<string, object> EventHeaders { get; set; }

        /// <summary>
        /// A collection of named headers related to the transaction.
        /// </summary>
        public IDictionary<string, object> TransactionHeaders { get; set; }
    }
}