using System;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Allows to configure event map how to handle custom actions using context <typeparamref name="TContext"/>.
    /// </summary>
    public interface IEventMapBuilder<TContext>
    {
        /// <summary>
        /// Builds the resulting event map. Can only be called once.
        /// </summary>
        IEventMap<TContext> Build(ProjectorMap<TContext> projector);
    }

    /// <summary>
    /// Handles custom actions asynchronously for an <see cref="IEventMap{TContext}"/>.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="projector">The delegate that must be invoked to handle the event.</param>
    public delegate Task CustomHandler<in TContext>(TContext context, Func<Task> projector);

    /// <summary>
    /// Allows to configure event map how to handle custom actions, projection creation, updating and deletion
    /// using context <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TProjection">Type of the projections.</typeparam>
    /// <typeparam name="TKey">Type of the projection keys.</typeparam>
    /// <typeparam name="TContext">Type of the context.</typeparam>
    public interface IEventMapBuilder<TProjection, TKey, TContext>
    {
        /// <summary>
        /// Builds the resulting event map. Can only be called once.
        /// </summary>
        IEventMap<TContext> Build(ProjectorMap<TProjection, TKey, TContext> projector);
    }

    /// <summary>
    /// Defines the contract for a projector that can handle the CRUD operations needed to handle
    /// events as mapped through the <see cref="EventMapBuilder{TContext}"/>.
    /// </summary>
    public class ProjectorMap<TContext>
    {
        public CustomHandler<TContext> Custom { get; set; } = (context, projector)
            => throw new NotSupportedException("No handler has been set-up for custom actions.");
    }

    /// <summary>
    /// Defines the contract for a projector that can handle the CRUD operations needed to handle
    /// events as mapped through the <see cref="EventMapBuilder{TProjection,Tkey,TContext}"/>
    /// </summary>
    public class ProjectorMap<TProjection, TKey, TContext> : ProjectorMap<TContext>
    {
        public CreationHandler<TProjection, TKey, TContext> Create { get; set; } = (key, context, projector, shouldOverwrite) =>
            throw new NotSupportedException("No handler has been set-up for creations.");

        public UpdateHandler<TProjection, TKey, TContext> Update { get; set; } = (key, context, projector, createIfMissing) =>
            throw new NotSupportedException("No handler has been set-up for updates.");

        public DeletionHandler<TKey, TContext> Delete { get; set; } = (key, context) =>
            throw new NotSupportedException("No handler has been set-up for deletions.");
    }

    /// <summary>
    /// Defines a handler for creating projections based on an event.
    /// </summary>
    /// <param name="key">
    /// The key of projection as extracted from the event during its mapping configuration.
    /// </param>
    /// <param name="context">
    /// An object providing information about the current event and any projector-specific metadata.
    /// </param>
    /// <param name="projector">
    /// The delegate that must be invoked to handle the event for the provided projection
    /// and modify the projection accordingly.
    /// </param>
    /// <param name="shouldOverwite">
    /// Should be called by the handler to determine how to handle existing projections by the same key.
    /// If it returns <c>true</c> then the handler should use the <paramref name="projector"/> to update the
    /// state of the existing projection, or <c>false</c> to ignore the call. Can throw an exception if that was
    /// requested through the event map. 
    /// </param>
    public delegate Task CreationHandler<out TProjection, TKey, in TContext>(
        TKey key,
        TContext context,
        Func<TProjection, Task> projector,
        Func<TProjection, bool> shouldOverwite);

    /// <summary>
    /// Defines a handler for updating projections based on an event.
    /// </summary>
    /// <param name="key">
    /// The key of projection as extracted from the event during its mapping configuration.
    /// </param>
    /// <param name="context">
    /// An object providing information about the current event and any projector-specific metadata.
    /// </param>
    /// <param name="projector">
    /// The delegate that must be invoked to handle the event for the provided projection
    /// and modify the projection accordingly.
    /// </param>
    /// <param name="createIfMissing">
    /// Should be called by the handler to determine whether it should create a missing projection. Depending on
    /// how the event was mapped, it can throw an exception that should not be caught by the projector.  
    /// </param>
    public delegate Task UpdateHandler<out TProjection, in TKey, in TContext>(
        TKey key,
        TContext context,
        Func<TProjection, Task> projector,
        Func<bool> createIfMissing);

   /// <summary>
   /// Defines a handler for deleting projections based on an event.
    /// </summary>
   /// <param name="key">
   /// The key of projection as extracted from the event during its mapping configuration.
   /// </param>
   /// <param name="context">
   /// An object providing information about the current event and any projector-specific metadata.
   /// </param>
    /// <returns>
    /// Returns a value indicating if deleting the projection succeeded. Should return <c>false</c> if the projection did not exist,
    /// </returns>
    public delegate Task<bool> DeletionHandler<in TKey, in TContext>(
        TKey key,
        TContext context);
}
   