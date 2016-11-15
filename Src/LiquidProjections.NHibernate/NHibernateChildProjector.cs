using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections.NHibernate
{
    /// <summary>
    /// Projects events to projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
    /// stored in a database accessed via NHibernate
    /// just before the parent projector in the same transaction.
    /// Throws <see cref="NHibernateProjectionException"/> when it detects known errors in the event handlers.
    /// </summary>
    public sealed class NHibernateChildProjector<TProjection, TKey> : INHibernateChildProjector
        where TProjection : class, IHaveIdentity<TKey>, new()
    {
        private readonly NHibernateEventMapConfigurator<TProjection, TKey> mapConfigurator;

        /// <summary>
        /// Creates a new instance of <see cref="NHibernateChildProjector{TProjection,TKey}"/>.
        /// </summary>
        /// <param name="mapBuilder">
        /// The <see cref="IEventMapBuilder{TProjection,TKey,TContext}"/>
        /// with already configured handlers for all the required events
        /// but not yet configured how to handle custom actions, projection creation, updating and deletion.
        /// The <see cref="IEventMap{TContext}"/> will be created from it.
        /// </param>
        /// <param name="children">An optional collection of <see cref="INHibernateChildProjector"/> which project events
        /// in the same transaction just before the parent projector.</param>
        public NHibernateChildProjector(
            IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> mapBuilder,
            IEnumerable<INHibernateChildProjector> children = null)
        {
            mapConfigurator = new NHibernateEventMapConfigurator<TProjection, TKey>(mapBuilder, children);
        }

        Task INHibernateChildProjector.ProjectEvent(object anEvent, NHibernateProjectionContext context)
        {
            return mapConfigurator.Handle(anEvent, context);
        }
    }
}