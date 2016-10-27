using NHibernate;

namespace LiquidProjections.NHibernate
{
    public sealed class NHibernateProjectionContext : ProjectionContext
    {
        public ISession Session { get; set; }
    }
}