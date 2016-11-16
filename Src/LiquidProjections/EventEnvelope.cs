using System;
using System.Collections.Generic;

namespace LiquidProjections
{
    /// <summary>
    /// Represents a single event including its headers.
    /// </summary>
    [Serializable]
    public class EventEnvelope
    {
        /// <summary>
        /// The actual event data.
        /// </summary>
        public object Body { get; set; }

        /// <summary>
        /// A collection of named headers related to the event.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; }
    }
}