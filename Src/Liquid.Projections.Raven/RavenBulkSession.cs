using System;
using System.Threading.Tasks;
using eVision.QueryHost.Raven.Dispatching;
using Raven.Abstractions.Data;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Indexes;

namespace eVision.QueryHost.Raven
{
    /// <summary>
    /// Implements basic session for big batch processing.
    /// </summary>
    public class RavenBulkSession : IWritableRavenSession
    {
        private readonly Func<IAsyncDocumentSession> sessionFactory;
        private readonly BulkInsertOperation bulk;
        private readonly LeastRecentlyUsedCache<string, IIdentity> cache;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="sessionFactory">Opens RavenDB session to load documents for update.</param>
        /// <param name="bulk">Bulk insert unit.</param>
        public RavenBulkSession(Func<IAsyncDocumentSession> sessionFactory, BulkInsertOperation bulk)
        {
            this.sessionFactory = sessionFactory;
            this.bulk = bulk;
            cache = new LeastRecentlyUsedCache<string, IIdentity>(capacity: 10 * 1000);
            cache.OnEvict += x => bulk.Store(x);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            bulk.Dispose();
        }

        /// <summary>
        /// Loads projection by its public id.
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <param name="id">Identity of the object.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        public async Task<T> Load<T>(string id) where T : IIdentity
        {
            var innerId = RavenSession.GetId<T>(id);

            IIdentity entity;
            if (cache.TryGetValue(innerId, out entity) == false)
            {
                using (var session = sessionFactory())
                {
                    entity = await session.LoadAsync<T>(innerId);
                    cache.Set(innerId, entity);
                }
            }
            return (T) entity;
        }

        /// <summary>
        /// Stores the Raven projection.
        /// </summary>
        /// <param name="obj">Instance to be stored.</param>
        /// <returns></returns>
        public Task Store(object obj)
        {
            bulk.Store(obj);
            return Task.FromResult(0);
        }

        /// <summary>
        /// Marks a projection to be removed on commit.
        /// </summary>
        /// <param name="entity"></param>
        public async Task Delete<T>(T entity) where T : IIdentity
        {
            IIdentity _;
            if (cache.TryRemoveValue(entity.Id, out _) == false)
            {
                await bulk.DatabaseCommands.DeleteAsync(entity.Id, null);
            }
        }

        /// <summary>
        /// Commits performed changes.
        /// </summary>
        /// <returns></returns>
        public Task SaveChanges()
        {
            foreach (var kvp in cache)
            {
                bulk.Store(kvp.Value);
            }
            return Task.FromResult(0);
        }

        /// <summary>
        /// Perform a set based deletes using the specified index, waits till the index is non-stale
        /// </summary>
        /// <typeparam name="TProjection">Projection to wait for index on</typeparam>
        /// <typeparam name="TIndex">The index implamentation</typeparam>
        public async Task DeleteByIndex<TProjection, TIndex>()
            where TProjection : IIdentity
            where TIndex : AbstractIndexCreationTask, new()
        {
            await bulk.DatabaseCommands.DeleteByIndexAsync(new TIndex().IndexName, new IndexQuery());
        }
    }
}