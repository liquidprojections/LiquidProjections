using System;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Defines the contract between event maps and projection storage providers.
    /// </summary>
    /// <typeparam name="TProjection">
    /// The type of projection this map applies to.
    /// </typeparam>
    /// <typeparam name="TContext">
    /// An object that provides additional information and metadata to the consuming projection code.
    /// </typeparam>
    public interface IEventMap<in TProjection, TContext>
    {
        /// <summary>
        /// Instructs the maps to forward all projection update requests to the provided handler.
        /// </summary>
        void ForwardUpdatesTo(UpdateHandler<TContext, TProjection> handler);

        /// <summary>
        /// Instructs the maps to forward all projection delete requests to the provided handler.
        /// </summary>
        void ForwardDeletesTo(DeleteHandler<TContext> handler);

        /// <summary>
        /// Instructs the maps to forward all other projection requests to the provided handler.
        /// </summary>
        void ForwardCustomActionsTo(CustomHandler<TContext> handler);

        /// <summary>
        /// Gets an asynchronous handler for <paramref name="event"/> or <c>null</c> if no handler
        /// has been registered.
        /// </summary>
        Func<TContext, Task> GetHandler(object @event);
    }

    public delegate Task UpdateHandler<TContext, out TProjection>(string key, TContext context, Func<TProjection, TContext, Task> projector);

    public delegate Task DeleteHandler<in TContext>(string key, TContext context);

    public delegate Task CustomHandler<TContext>(TContext context, Func<TContext, Task> projector);

}