using System;
using System.Runtime.Serialization;

namespace LiquidProjections.RavenDB
{
    /// <summary>
    /// An exception describing an unrecoverable error in a projector.
    /// </summary>
    [Serializable]
    public class RavenProjectionException : Exception
    {
        internal RavenProjectionException(string message)
            : base(message)
        {
        }

        internal RavenProjectionException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected RavenProjectionException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}