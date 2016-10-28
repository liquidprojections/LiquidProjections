using System;
using System.Runtime.Serialization;

namespace LiquidProjections.NHibernate
{
    [Serializable]
    public class NHibernateProjectorException : Exception
    {
        internal NHibernateProjectorException(string message)
            : base(message)
        {
        }

        internal NHibernateProjectorException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected NHibernateProjectorException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}