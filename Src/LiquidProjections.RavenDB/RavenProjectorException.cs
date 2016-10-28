using System;
using System.Runtime.Serialization;

namespace LiquidProjections.RavenDB
{
    [Serializable]
    public class RavenProjectorException : Exception
    {
        internal RavenProjectorException(string message)
            : base(message)
        {
        }

        internal RavenProjectorException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected RavenProjectorException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}