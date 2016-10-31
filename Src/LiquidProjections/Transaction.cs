using System;
using System.Collections.Generic;

namespace LiquidProjections
{
    /// <summary>
    /// Represents a collection of events that have happened within the same transactional boundary. 
    /// </summary>
    public class Transaction
    {
        public Transaction()
        {
            Events = new List<EventEnvelope>();
        }

        /// <summary>
        /// Gets the point in time at which the transaction was persisted.
        /// </summary>
        public DateTime TimeStampUtc { get; set; }

        /// <summary>
        /// Gets the value which uniquely identifies the transaction over all streams.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets the value which uniquely identifies the stream of events to which the transaction belongs.
        /// </summary>
        public string StreamId { get; set; }

        /// <summary>
        /// Gets the collection of events that were persisted together.
        /// </summary>
        public ICollection<EventEnvelope> Events { get; set; }

        /// <summary>
        /// The checkpoint that represents the storage level order.
        /// </summary>
        public long Checkpoint { get; set; }

        /// <summary>
        /// A collection of named headers related to the transaction.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; }
    }
}