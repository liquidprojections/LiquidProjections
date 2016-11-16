using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections.NHibernate
{
    internal sealed class NHibernateEventMapConfigurator<TProjection, TKey> : IEventMap<NHibernateProjectionContext>
        where TProjection : class, IHaveIdentity<TKey>, new()
    {
        private readonly IEventMap<NHibernateProjectionContext> map;
        private readonly IEnumerable<INHibernateChildProjector> children;

        public NHibernateEventMapConfigurator(
            IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> mapBuilder,
            IEnumerable<INHibernateChildProjector> children = null)
        {
            if (mapBuilder == null)
            {
                throw new ArgumentNullException(nameof(mapBuilder));
            }

            map = BuildMap(mapBuilder);
            this.children = children?.ToList() ?? new List<INHibernateChildProjector>();
        }

        private IEventMap<NHibernateProjectionContext> BuildMap(
            IEventMapBuilder<TProjection, TKey, NHibernateProjectionContext> mapBuilder)
        {
            mapBuilder.HandleCustomActionsAs((_, projector) => projector());
            mapBuilder.HandleProjectionModificationsAs(HandleProjectionModification);
            mapBuilder.HandleProjectionDeletionsAs(HandleProjectionDeletion);
            return mapBuilder.Build();
        }

        private async Task HandleProjectionModification(TKey key, NHibernateProjectionContext context,
            Func<TProjection, Task> projector, ProjectionModificationOptions options)
        {
            TProjection projection = context.Session.Get<TProjection>(key);

            if (projection == null)
            {
                switch (options.MissingProjectionBehavior)
                {
                    case MissingProjectionModificationBehavior.Create:
                    {
                        projection = new TProjection { Id = key };
                        await projector(projection);
                        context.Session.Save(projection);
                        break;
                    }

                    case MissingProjectionModificationBehavior.Ignore:
                    {
                        break;
                    }

                    case MissingProjectionModificationBehavior.Throw:
                    {
                        throw new NHibernateProjectionException(
                            $"Projection {typeof(TProjection)} with key {key} does not exist.");
                    }

                    default:
                    {
                        throw new NotSupportedException(
                            $"Not supported missing projection behavior {options.MissingProjectionBehavior}.");
                    }
                }
            }
            else
            {
                switch (options.ExistingProjectionBehavior)
                {
                    case ExistingProjectionModificationBehavior.Update:
                    {
                        await projector(projection);
                        break;
                    }

                    case ExistingProjectionModificationBehavior.Ignore:
                    {
                        break;
                    }

                    case ExistingProjectionModificationBehavior.Throw:
                    {
                        throw new NHibernateProjectionException(
                            $"Projection {typeof(TProjection)} with key {key} already exists.");
                    }

                    default:
                    {
                        throw new NotSupportedException(
                            $"Not supported existing projection behavior {options.ExistingProjectionBehavior}.");
                    }
                }
            }
        }

        private Task HandleProjectionDeletion(TKey key, NHibernateProjectionContext context,
            ProjectionDeletionOptions options)
        {
            TProjection existingProjection = context.Session.Get<TProjection>(key);

            if (existingProjection == null)
            {
                switch (options.MissingProjectionBehavior)
                {
                    case MissingProjectionDeletionBehavior.Ignore:
                    {
                        break;
                    }

                    case MissingProjectionDeletionBehavior.Throw:
                    {
                        throw new NHibernateProjectionException(
                            $"Cannot delete {typeof(TProjection)} projection with key {key}. The projection does not exist.");
                    }

                    default:
                    {
                        throw new NotSupportedException(
                            $"Not supported missing projection behavior {options.MissingProjectionBehavior}.");
                    }
                }
            }
            else
            {
                context.Session.Delete(existingProjection);
            }

            return Task.FromResult(false);
        }

        public async Task Handle(object anEvent, NHibernateProjectionContext context)
        {
            foreach (INHibernateChildProjector child in children)
            {
                await child.ProjectEvent(anEvent, context);
            }

            await map.Handle(anEvent, context);
        }
    }
}