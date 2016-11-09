using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    /// <summary>
    /// Projects events to projections stored in RavenDB
    /// just before the parent projector in the same session.
    /// </summary>
    public interface IRavenChildProjector
    {
        /// <summary>
        /// Asynchronously projects event <paramref name="anEvent"/> using context <paramref name="context"/>.
        /// </summary>
        Task ProjectEvent(object anEvent, RavenProjectionContext context);
    }
}