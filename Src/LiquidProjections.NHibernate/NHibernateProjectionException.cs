using System;
using System.Runtime.Serialization;

namespace LiquidProjections.NHibernate
{
    /// <summary>
    /// An exception describing an unrecoverable error in a projector.
    /// </summary>
    [Serializable]
    public class NHibernateProjectionException : Exception
    {
        internal NHibernateProjectionException(string message)
            : base(message)
        {
        }

        internal NHibernateProjectionException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected NHibernateProjectionException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}