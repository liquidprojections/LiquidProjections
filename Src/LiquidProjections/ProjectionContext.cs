using System;
using System.Collections.Generic;

namespace LiquidProjections
{
    /// <summary>
    /// Provides contextual information about projecting a transaction.
    /// </summary>
    public class ProjectionContext
    {
        /// <summary>
        /// Gets the value which uniquely identifies the transaction.
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// Gets the value which uniquely identifies the stream to which the transaction belongs.
        /// </summary>
        public string StreamId { get; set; }

        /// <summary>
        /// Gets or sets the point in time at which the transaction that is currently being dispatched was persisted.
        /// </summary>
        public DateTime TimeStampUtc { get; set; }

        /// <summary>
        /// Gets or sets the checkpoint of the transaction that is currently being dispatched.
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