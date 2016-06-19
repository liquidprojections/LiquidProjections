using System;
using System.Collections.Generic;

namespace eVision.QueryHost
{
    /// <summary>
    /// Represents a collection of events that have happened within the same transactional boundary. 
    /// </summary>
    public class Transaction
    {
        public Transaction()
        {
            Events = new List<Envelope>();
        }

        /// <summary>
        /// Gets the point in time at which the transaction was persisted.
        /// </summary>
        public DateTime TimeStamp { get; set; }

        /// <summary>
        /// Gets the value which uniquely identifies the transaction over all streams.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets the value which uniquely identifies the stream to which the transaction belongs.
        /// </summary>
        public string StreamId { get; set; }
        
        /// <summary>
        /// Gets the collection of events that were persisted together.
        /// </summary>
        public ICollection<Envelope> Events { get; set; }

        /// <summary>
        /// The checkpoint that represents the storage level order.
        /// </summary>
        public string Checkpoint { get; set; }
    }
}