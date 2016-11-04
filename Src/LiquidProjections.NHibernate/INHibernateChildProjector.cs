using System.Threading.Tasks;

namespace LiquidProjections.NHibernate
{
    /// <summary>
    /// Projects events to projections stored in a database accessed via NHibernate
    /// just before the parent projector in the same transaction.
    /// </summary>
    public interface INHibernateChildProjector
    {
        /// <summary>
        /// Asynchronously projects event <paramref name="anEvent"/> using context <paramref name="context"/>.
        /// </summary>
        Task ProjectEvent(object anEvent, NHibernateProjectionContext context);
    }
}