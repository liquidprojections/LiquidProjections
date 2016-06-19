using System;
using System.Threading.Tasks;

namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// Base class to implement RavenDB based projector.
    /// </summary>
    /// <typeparam name="TProjection"></typeparam>
    public abstract class RavenProjector<TProjection>
        where TProjection : IIdentity, new()
    {
        private readonly IWritableRavenSession session;
        private readonly RavenLookupCollection<TProjection> lookups;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="session"></param>
        protected RavenProjector(IWritableRavenSession session)
        {
            this.session = session;
            lookups = new RavenLookupCollection<TProjection>(session);
        }

        /// <summary>
        /// Registers a lookup for a <c>TProjection</c>.
        /// </summary>
        /// <param name="lookupKeySelector">
        /// Gets the unique key of the lookup, usually a single property of projection.
        ///  NULL means no lookup required.
        /// </param>
        /// <typeparam name="TLookup">Type of a lookup.</typeparam>
        protected void WithLookup<TLookup>(Func<TProjection, string> lookupKeySelector)
            where TLookup : RavenLookup, new()
        {
            lookups.WithLookup<TLookup>(lookupKeySelector);
        }

        /// <summary>
        /// Finds or creates a projection identified by the provided <paramref name="identity"/> and passes it
        /// to a user-provided <paramref name="action"/>. 
        /// </summary>
        /// <remarks>
        /// Ensures that the action is only executed if the <paramref name="version"/> exceeds the version of 
        /// the projection.
        /// </remarks>
        protected async Task OnHandle(string identity, long version, Action<TProjection> action, MissingProjectionBehavior missingProjectionBehavior = MissingProjectionBehavior.Create)
        {
            await OnHandle(identity, version, p =>
            {
                action(p);
                return Task.FromResult(0);
            }, missingProjectionBehavior);
        }

        /// <summary>
        /// Finds or creates a projection identified by the provided <paramref name="identity"/> and passes it
        /// to a user-provided <paramref name="asyncAction"/>. 
        /// </summary>
        /// <remarks>
        /// Ensures that the action is only executed if the <paramref name="version"/> exceeds the version of 
        /// the projection.
        /// </remarks>
        protected async Task OnHandle(string identity, long version, Func<TProjection, Task> asyncAction, MissingProjectionBehavior missingProjectionBehavior = MissingProjectionBehavior.Create)
        {
            if (string.IsNullOrEmpty(identity))
            {
                throw new InvalidOperationException("Don't know which projection to projector, missing its identity.");
            }

            TProjection projection = await session.Load<TProjection>(identity);
            if (projection == null)
            {
                if (missingProjectionBehavior == MissingProjectionBehavior.Create)
                {
                    projection = await CreateNewProjection(identity, version, asyncAction);

                    await session.Store(projection);
                }
            }
            else
            {
                await UpdateProjection(version, async p =>
                {
                    var updateLookups = lookups.CreateLookupUpdater(p.Id, p);
                    await asyncAction(p);
                    await updateLookups(p);
                }, projection);
            }
        }

        private async Task<TProjection> CreateNewProjection(string identity, long version, Func<TProjection, Task> asyncAction)
        {
            var projection = new TProjection
            {
                Id = RavenSession.GetId<TProjection>(identity)
            };
            var versionedProjection = projection as IHaveVersion;
            if (versionedProjection != null)
            {
                versionedProjection.Version = version;
            }

            var updateLookups = lookups.CreateLookupUpdater(projection.Id, projection);
            await asyncAction(projection);
            await updateLookups(projection);

            return projection;
        }

        private async Task UpdateProjection(long version, Func<TProjection, Task> action, TProjection projection)
        {
            var versionedProjection = projection as IHaveVersion;
            if (versionedProjection != null && version != Constants.IgnoredVersion)
            {
                if ((version > versionedProjection.Version))
                {
                    long maxVersion = Math.Max(versionedProjection.Version, version);

                    await action(projection);

                    versionedProjection.Version = maxVersion;
                }
            }
            else
            {
                await action(projection);
            }
        }

        /// <summary>
        /// Finds an existing projection identified by the provided <paramref name="identity"/> and removes it.
        /// </summary>
        /// <remarks>
        /// Ensures that the projection is only removed if the <paramref name="version"/> exceeds the version of 
        /// the projection.
        /// </remarks>
        protected async Task RemoveProjection(string identity, long version)
        {
            var projection = await session.Load<TProjection>(identity);
            if (projection != null)
            {
                await UpdateProjection(version, async p =>
                {
                    Func<TProjection, Task> updateLookups = lookups.CreateLookupUpdater(projection.Id, projection);
                    await session.Delete(p);
//                    await updateLookups(null);
                }, projection);
            }
        }
    }
}