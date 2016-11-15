using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections.RavenDB
{
    internal sealed class RavenEventMapConfigurator<TProjection>
        where TProjection : class, IHaveIdentity, new()
    {
        private readonly IEventMap<RavenProjectionContext> map;
        private IProjectionCache cache = new PassthroughCache();
        private string collectionName = typeof(TProjection).Name;
        private readonly IEnumerable<IRavenChildProjector> children;

        public RavenEventMapConfigurator(
            IEventMapBuilder<TProjection, string, RavenProjectionContext> mapBuilder,
            IEnumerable<IRavenChildProjector> children = null)
        {
            if (mapBuilder == null)
            {
                throw new ArgumentNullException(nameof(mapBuilder));
            }

            map = BuildMap(mapBuilder);
            this.children = children?.ToList() ?? new List<IRavenChildProjector>();
        }

        public string CollectionName
        {
            get { return collectionName; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Collection name is missing.", nameof(value));
                }

                collectionName = value;
            }
        }

        public IProjectionCache Cache
        {
            get { return cache; }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                cache = value;
            }
        }

        private IEventMap<RavenProjectionContext> BuildMap(
            IEventMapBuilder<TProjection, string, RavenProjectionContext> mapBuilder)
        {
            mapBuilder.HandleCustomActionsAs((_, projector) => projector());
            mapBuilder.HandleProjectionModificationsAs(HandleProjectionModification);
            mapBuilder.HandleProjectionDeletionsAs(HandleProjectionDeletion);
            return mapBuilder.Build();
        }

        private async Task HandleProjectionModification(string key, RavenProjectionContext context,
            Func<TProjection, Task> projector, ProjectionModificationOptions options)
        {
            string databaseId = BuildDatabaseId(key);
            var projection = (TProjection)await cache.TryGet(databaseId).ConfigureAwait(false);

            if (projection == null)
            {
                projection = await context.Session.LoadAsync<TProjection>(databaseId).ConfigureAwait(false);

                if (projection != null)
                {
                    cache.Add(projection);
                }
            }

            if (projection == null)
            {
                switch (options.MissingProjectionBehavior)
                {
                    case MissingProjectionModificationBehavior.Create:
                    {
                        projection = new TProjection { Id = databaseId };
                        await projector(projection).ConfigureAwait(false);
                        cache.Add(projection);
                        await context.Session.StoreAsync(projection).ConfigureAwait(false);
                        break;
                    }

                    case MissingProjectionModificationBehavior.Ignore:
                    {
                        break;
                    }

                    case MissingProjectionModificationBehavior.Throw:
                    {
                        throw new ProjectionException($"Projection with id {databaseId} does not exist.");
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
                        await projector(projection).ConfigureAwait(false);
                        await context.Session.StoreAsync(projection).ConfigureAwait(false);
                        break;
                    }

                    case ExistingProjectionModificationBehavior.Ignore:
                    {
                        break;
                    }

                    case ExistingProjectionModificationBehavior.Throw:
                    {
                        throw new ProjectionException($"Projection with id {databaseId} already exists.");
                    }

                    default:
                    {
                        throw new NotSupportedException(
                            $"Not supported existing projection behavior {options.ExistingProjectionBehavior}.");
                    }
                }
            }
        }

        private async Task HandleProjectionDeletion(string key, RavenProjectionContext context,
            ProjectionDeletionOptions options)
        {
            string databaseId = BuildDatabaseId(key);

            // If the projection is already loaded, we have to delete it via the loaded instance.
            // If the projection is not cached, we have to load it to verify that it exists.
            // Otherwise we can delete fast by id without loading the projection.
            if (context.Session.Advanced.IsLoaded(databaseId) || !await IsCached(databaseId).ConfigureAwait(false))
            {
                TProjection projection = await context.Session.LoadAsync<TProjection>(databaseId).ConfigureAwait(false);

                if (projection == null)
                {
                    switch (options.MissingProjectionBehavior)
                    {
                        case MissingProjectionDeletionBehavior.Ignore:
                        {
                            break;
                        }

                        case MissingProjectionDeletionBehavior.Throw:
                        {
                            throw new ProjectionException(
                                $"Cannot delete projection with id {databaseId}. The projection does not exist.");
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
                    context.Session.Delete(projection);
                    cache.Remove(databaseId);
                }
            }
            else
            {
                context.Session.Delete(databaseId);
                cache.Remove(databaseId);
            }
        }

        private string BuildDatabaseId(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Aggregate key is missing.");
            }

            return $"{collectionName}/{key}";
        }

        private async Task<bool> IsCached(string databaseId)
        {
            TProjection cachedProjection = (TProjection)await cache.TryGet(databaseId).ConfigureAwait(false);
            return cachedProjection != null;
        }

        public async Task ProjectEvent(object anEvent, RavenProjectionContext context)
        {
            foreach (IRavenChildProjector child in children)
            {
                await child.ProjectEvent(anEvent, context).ConfigureAwait(false);
            }

            await map.Handle(anEvent, context).ConfigureAwait(false);
        }
    }
}