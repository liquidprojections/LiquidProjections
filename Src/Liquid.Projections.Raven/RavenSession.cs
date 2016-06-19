using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

using eVision.QueryHost.Raven.Dispatching;
using eVision.QueryHost.Raven.Querying;

using Raven.Abstractions.Data;
using Raven.Abstractions.Util;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Indexes;
using Raven.Client.Linq;

namespace eVision.QueryHost.Raven
{
    /// <summary>
    /// Base implementation of the Raven session that is aware of ID conventions.
    /// </summary>
    public abstract class RavenSession : IWritableRavenSession, IQueryableRavenSession
    {
        private IAsyncDocumentSession session;

        /// <summary>
        /// Base constructor of the RavenSession wrapper.
        /// </summary>
        /// <param name="session"></param>
        protected RavenSession(IAsyncDocumentSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// Dynamically queries RavenDB using LINQ 
        /// </summary>
        /// <typeparam name="T">The result of the query </typeparam>
        public IRavenQueryable<T> Query<T>() where T : IIdentity
        {
            return session.Query<T>();
        }

        /// <summary>
        /// Queries the index specified by <typeparamref name="TIndexCreator"/> using Linq.
        /// </summary>
        /// <typeparam name="T">The result of the query</typeparam>
        /// <typeparam name="TIndexCreator">The type of the index creator.</typeparam>
        /// <returns/>
        public IRavenQueryable<T> Query<T, TIndexCreator>()
            where TIndexCreator : AbstractIndexCreationTask, new()
        {
            return session.Query<T, TIndexCreator>();
        }

        /// <summary>
        /// Begin a load while including the specified path
        /// </summary>
        /// <param name="path">The path.</param>
        public IAsyncLoaderWithInclude<object> Include(string path)
        {
            return session.Include(path);
        }

        /// <summary>
        /// Begin a load while including the specified path
        /// </summary>
        /// <param name="path">The path.</param>
        public IAsyncLoaderWithInclude<T> Include<T>(Expression<Func<T, object>> path)
        {
            return session.Include(path);
        }

        /// <summary>
        /// Begin a load while including the specified path
        /// </summary>
        /// <param name="path">The path.</param>
        public IAsyncLoaderWithInclude<T> Include<T, TInclude>(Expression<Func<T, object>> path)
        {
            return session.Include<T, TInclude>(path);
        }

        /// <summary>
        /// Get the accessor for advanced operations
        /// </summary>
        /// <remarks>
        /// Those operations are rarely needed, and have been moved to a separate property to avoid cluttering the API
        /// </remarks>
        public IAsyncAdvancedSessionOperations Advanced
        {
            get { return session.Advanced; }
        }

        /// <summary>
        /// Pages over result set end returns it full.
        /// </summary>
        /// <typeparam name="T">The result of the query </typeparam>
        /// <typeparam name="TIndexCreator">The type of the index creator.</typeparam>
        /// <param name="whereClause">Limiting where clause conditions.</param>
        /// <returns>Async unbounded result set containing evrything.</returns>
        public async Task<IEnumerable<T>> Stream<T, TIndexCreator>(Func<IRavenQueryable<T>, IQueryable<T>> whereClause)
            where TIndexCreator : AbstractIndexCreationTask, new()
        {
            var count = await whereClause(Query<T, TIndexCreator>().Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())).CountAsync();
            var results = new List<T>(count);
            using (IAsyncEnumerator<StreamResult<T>> result = await session.Advanced.StreamAsync(whereClause(Query<T, TIndexCreator>())))
            {
                while (await result.MoveNextAsync())
                {
                    results.Add(result.Current.Document);
                }
            }
            return results;
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
            await session.Query<TProjection, TIndex>().Customize(x => x.WaitForNonStaleResultsAsOfNow()).CountAsync();
            await session.Advanced.DocumentStore.AsyncDatabaseCommands.DeleteByIndexAsync(new TIndex().IndexName, new IndexQuery());
        }

        /// <summary>
        /// Marks the specified entity for deletion. The entity will be deleted when <see cref="M:Raven.Client.IAsyncDocumentSession.SaveChangesAsync"/> is called.
        /// </summary>
        /// <typeparam name="T"/><param name="entity">The entity.</param>
        public Task Delete<T>(T entity)
            where T : IIdentity
        {
            session.Delete(entity);
            return Task.FromResult(0);
        }

        /// <summary>
        /// Begins the async save changes operation
        /// </summary>
        /// <returns/>
        public Task SaveChanges()
        {
            return session.SaveChangesAsync();
        }

        /// <summary>
        /// Loads projection by its public id.
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <param name="id">Identity of the object.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        public Task<T> Load<T>(string id) where T : IIdentity
        {
            return session.LoadAsync<T>(GetId<T>(id));
        }

        /// <summary>
        /// Begins the a load that will use the specified results transformer against the specified id
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <typeparam name="TResult">Type to present transformation results.</typeparam>
        /// <typeparam name="TTransformer">Transformer to be applied on the result.</typeparam>
        /// <param name="id">Identity of the object.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        public Task<TResult> Load<TTransformer, T, TResult>(string id)
            where TTransformer : AbstractTransformerCreationTask<T>, new()
            where T : IIdentity
        {
            return session.LoadAsync<TTransformer, TResult>(GetId<T>(id));
        }

        /// <summary>
        /// Loads projection by its public id.
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <param name="ids">Identities of the objects.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        public Task<T[]> Load<T>(IEnumerable<string> ids) where T : IIdentity
        {
            return session.LoadAsync<T>(ids.Select(GetId<T>));
        }

        /// <summary>
        /// Begins the a load that will use the specified results transformer against the specified id
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <typeparam name="TResult">Type to present transformation results.</typeparam>
        /// <typeparam name="TTransformer">Transformer to be applied on the result.</typeparam>
        /// <param name="ids">Identities of the objects.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        public Task<TResult[]> Load<TTransformer, T, TResult>(IEnumerable<string> ids)
            where TTransformer : AbstractTransformerCreationTask<T>, new()
            where T : IIdentity
        {
            return session.LoadAsync<TTransformer, TResult>(ids.Select(GetId<T>));
        }

        /// <summary>
        /// Stores the Raven projection.
        /// </summary>
        /// <param name="obj">Instance to be stored.</param>
        /// <returns></returns>
        public Task Store(object obj)
        {
            return session.StoreAsync(obj);
        }

        /// <summary>
        /// Disposes wrapped session.
        /// </summary>
        public void Dispose()
        {
            if (session != null)
            {
                session.Dispose();
                session = null;
            }
        }

        /// <summary>
        /// Converts public identity of the Raven Projection to an internal one, which is unique globally per database.
        /// </summary>
        /// <param name="id">Public id of the projection.</param>
        /// <typeparam name="T">Projection type.</typeparam>
        /// <returns>Internal identity which is stored in RavenDB.</returns>
        public static string GetId<T>(string id) where T : IIdentity
        {
            var prefix = string.Format("{0}/", typeof(T).Name);
            return id.StartsWith(prefix) ? id : prefix + id;
        }
    }
}