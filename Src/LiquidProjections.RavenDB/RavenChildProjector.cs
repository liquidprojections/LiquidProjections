using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    /// <summary>
    /// Projects events to projections of type <typeparamref name="TProjection"/> stored in RavenDB
    /// just before the parent projector in the same session.
    /// Throws <see cref="RavenProjectionException"/> when it detects known errors in the event handlers.
    /// </summary>
    public class RavenChildProjector<TProjection> : IRavenChildProjector
        where TProjection : class, IHaveIdentity, new()
    {
        private readonly RavenEventMapConfigurator<TProjection> mapConfigurator;

        /// <summary>
        /// Creates a new instance of <see cref="RavenChildProjector{TProjection}"/>.
        /// </summary>
        /// <param name="mapBuilder">
        /// The <see cref="IEventMapBuilder{TProjection,TKey,TContext}"/>
        /// with already configured handlers for all the required events
        /// but not yet configured how to handle custom actions, projection creation, updating and deletion.
        /// The <see cref="IEventMap{TContext}"/> will be created from it.
        /// </param>
        /// <param name="children">An optional collection of <see cref="IRavenChildProjector"/> which project events
        /// in the same session just before the parent projector.</param>
        public RavenChildProjector(
            IEventMapBuilder<TProjection, string, RavenProjectionContext> mapBuilder,
            IEnumerable<IRavenChildProjector> children = null)
        {
            mapConfigurator = new RavenEventMapConfigurator<TProjection>(mapBuilder, children);
        }

        /// <summary>
        /// The name of the collection in RavenDB that contains the projections.
        /// Defaults to the name of the projection type <typeparamref name="TProjection"/>.
        /// Is also used as the document name of the projector state in RavenCheckpoints collection.
        /// </summary>
        public string CollectionName
        {
            get { return mapConfigurator.CollectionName; }
            set { mapConfigurator.CollectionName = value; }
        }

        /// <summary>
        /// A cache that can be used to avoid loading projections from the database.
        /// </summary>
        public IProjectionCache Cache
        {
            get { return mapConfigurator.Cache; }
            set { mapConfigurator.Cache = value; }
        }

        Task IRavenChildProjector.ProjectEvent(object anEvent, RavenProjectionContext context)
        {
            if (anEvent == null)
            {
                throw new ArgumentNullException(nameof(anEvent));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return mapConfigurator.ProjectEvent(anEvent, context);
        }
    }
}