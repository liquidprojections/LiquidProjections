using System;
using System.Collections.Generic;

namespace LiquidProjections
{
    /// <summary>
    /// Provides contextual information about projecting an event.
    /// </summary>
    public class ProjectionContext
    {
        /// <summary>
        /// The value which uniquely identifies the transaction which contains the event.
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// The value which uniquely identifies the stream to which the event belongs.
        /// </summary>
        public string StreamId { get; set; }

        /// <summary>
        /// The point in time at which the event was persisted.
        /// </summary>
        public DateTime TimeStampUtc { get; set; }

        /// <summary>
        /// The checkpoint of the transaction which contains the event.
        /// </summary>
        public long Checkpoint { get; set; }

        /// <summary>
        /// A collection of named headers related to the event.
        /// </summary>
        public IDictionary<string, object> EventHeaders { get; set; }

        /// <summary>
        /// A collection of named headers related to the transaction which contains the event.
        /// </summary>
        public IDictionary<string, object> TransactionHeaders { get; set; }
    }
}