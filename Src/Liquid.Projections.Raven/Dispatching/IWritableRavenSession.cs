using System;
using System.Threading.Tasks;

using Raven.Client.Indexes;

namespace eVision.QueryHost.Raven.Dispatching
{
    /// <summary>
    /// General around Raven Session interface to perform changes on projection.
    /// </summary>
    public interface IWritableRavenSession : IDisposable
    {
        /// <summary>
        /// Loads projection by its public id.
        /// </summary>
        /// <typeparam name="T">Exact type to represent Raven Projection.</typeparam>
        /// <param name="id">Identity of the object.</param>
        /// <returns>Instance from the RavenDB storage.</returns>
        Task<T> Load<T>(string id) where T : IIdentity;

        /// <summary>
        /// Stores the Raven projection.
        /// </summary>
        /// <param name="obj">Instance to be stored.</param>
        /// <returns></returns>
        Task Store(object obj);

        /// <summary>
        /// Marks a projection to be removed on commit.
        /// </summary>
        /// <param name="entity"></param>
        Task Delete<T>(T entity) where T : IIdentity;

        /// <summary>
        /// Commits performed changes.
        /// </summary>
        /// <returns></returns>
        Task SaveChanges();

        /// <summary>
        /// Perform a set based deletes using the specified index, waits till the index is non-stale
        /// </summary>
        /// <typeparam name="TProjection">Projection to wait for index on</typeparam>
        /// <typeparam name="TIndex">The index implamentation</typeparam>
        Task DeleteByIndex<TProjection, TIndex>()
            where TProjection : IIdentity
            where TIndex : AbstractIndexCreationTask, new();
    }
}