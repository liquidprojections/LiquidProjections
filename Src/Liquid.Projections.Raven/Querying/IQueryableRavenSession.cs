using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using eVision.QueryHost.Raven.Dispatching;
using Raven.Client;
using Raven.Client.Document;
using Raven.Client.Indexes;
using Raven.Client.Linq;

namespace eVision.QueryHost.Raven.Querying
{
    /// <summary>
    /// Base contract for the Raven session for read.
    /// </summary>
    public interface IQueryableRavenSession : IDisposable
    {
        /// <summary>
        /// Loads projection by its public id.
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <param name="id">Identity of the object.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        Task<T> Load<T>(string id) where T : IIdentity;

        /// <summary>
        /// Begins the a load that will use the specified results transformer against the specified id
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <typeparam name="TResult">Type to present transformation results.</typeparam>
        /// <typeparam name="TTransformer">Transformer to be applied on the result.</typeparam>
        /// <param name="id">Identity of the object.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        Task<TResult> Load<TTransformer, T, TResult>(string id)
            where T : IIdentity
            where TTransformer : AbstractTransformerCreationTask<T>, new();

        /// <summary>
        /// Loads projection by its public id.
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <param name="ids">Identities of the objects.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        Task<T[]> Load<T>(IEnumerable<string> ids) where T : IIdentity;

        /// <summary>
        /// Begins the a load that will use the specified results transformer against the specified id
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <typeparam name="TResult">Type to present transformation results.</typeparam>
        /// <typeparam name="TTransformer">Transformer to be applied on the result.</typeparam>
        /// <param name="ids">Identities of the objects.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        Task<TResult[]> Load<TTransformer, T, TResult>(IEnumerable<string> ids)
            where T : IIdentity
            where TTransformer : AbstractTransformerCreationTask<T>, new();

        /// <summary>
        /// Dynamically queries RavenDB using LINQ 
        /// </summary>
        /// <typeparam name="T">The result of the query </typeparam>
        IRavenQueryable<T> Query<T>() where T : IIdentity;

        /// <summary>
        /// Queries the index specified by <typeparamref name="TIndexCreator"/> using Linq.
        /// </summary>
        /// <typeparam name="T">The result of the query</typeparam><typeparam name="TIndexCreator">The type of the index creator.</typeparam>
        /// <returns/>
        IRavenQueryable<T> Query<T, TIndexCreator>()
            where TIndexCreator : AbstractIndexCreationTask, new();

        /// <summary>
        /// Begin a load while including the specified path
        /// </summary>
        /// <param name="path">The path.</param>
        IAsyncLoaderWithInclude<object> Include(string path);

        /// <summary>
        /// Begin a load while including the specified path
        /// </summary>
        /// <param name="path">The path.</param>
        IAsyncLoaderWithInclude<T> Include<T>(Expression<Func<T, object>> path);

        /// <summary>
        /// Begin a load while including the specified path
        /// </summary>
        /// <param name="path">The path.</param>
        IAsyncLoaderWithInclude<T> Include<T, TInclude>(Expression<Func<T, object>> path);

        /// <summary>
        /// Get the accessor for advanced operations
        /// </summary>
        /// <remarks>
        /// Those operations are rarely needed, and have been moved to a separate property to avoid cluttering the API
        /// </remarks>
        IAsyncAdvancedSessionOperations Advanced { get; }

        /// <summary>
        /// Pages over result set end returns it full.
        /// </summary>
        /// <typeparam name="T">The result of the query </typeparam>
        /// <typeparam name="TIndexCreator">The type of the index creator.</typeparam>
        /// <param name="whereClause">Limiting where clause conditions.</param>
        /// <returns>Async unbounded result set containing evrything.</returns>
        Task<IEnumerable<T>> Stream<T, TIndexCreator>(Func<IRavenQueryable<T>, IQueryable<T>> whereClause)
            where TIndexCreator : AbstractIndexCreationTask, new();
    }
}