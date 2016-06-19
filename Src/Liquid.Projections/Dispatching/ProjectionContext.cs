using System;
using System.Collections.Generic;

namespace eVision.QueryHost.Dispatching
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
        public DateTime CommitStamp { get; set; }

        /// <summary>
        /// Gets or sets the checkpoint of the commit that is currently being dispatched.
        /// </summary>
        public string CheckPoint { get; set; }

        /// <summary>
        /// Gets or sets the metadata which provides additional, unstructured information about the commit that is currently being dispatched.
        /// </summary>
        public IDictionary<string, object> Headers { get; set; }
    }
}