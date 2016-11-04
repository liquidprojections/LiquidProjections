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
        IEventMap<TContext> Build();

        /// <summary>
        /// Configures the event map to handle custom actions via the provided delegate <paramref name="handler"/>.
        /// </summary>
        void HandleCustomActionsAs(CustomHandler<TContext> handler);
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
    public interface IEventMapBuilder<in TProjection, out TKey, TContext> : IEventMapBuilder<TContext>
    {
        /// <summary>
        /// Configures the event map to handle projection creation and updating
        /// via the provided delegate <paramref name="handler"/>.
        /// </summary>
        void HandleProjectionModificationsAs(ProjectionModificationHandler<TProjection, TKey, TContext> handler);

        /// <summary>
        /// Configures the event map to handle projection deletion
        /// via the provided delegate <paramref name="handler"/>.
        /// </summary>
        void HandleProjectionDeletionsAs(ProjectionDeletionHandler<TKey, TContext> handler);
    }

    /// <summary>
    /// Handles projection creation and updating asynchronously using context <see cref="IEventMap{TContext}"/>.
    /// </summary>
    /// <typeparam name="TProjection">Type of the projections.</typeparam>
    /// <typeparam name="TKey">Type of the projection keys.</typeparam>
    /// <typeparam name="TContext">Type of the context.</typeparam>
    /// <param name="key">Key of the projection.</param>
    /// <param name="context">The context.</param>
    /// <param name="projector">
    /// The delegate that must be invoked to handle the event for the provided projection
    /// and modify the projection accordingly.
    /// </param>
    /// <param name="options">Additional options <see cref="ProjectionModificationOptions"/>.</param>
    public delegate Task ProjectionModificationHandler<out TProjection, in TKey, in TContext>(
        TKey key,
        TContext context,
        Func<TProjection, Task> projector,
        ProjectionModificationOptions options);

    /// <summary>
    /// Provides additional options for <see cref="ProjectionModificationHandler{TProjection,TKey,TContext}"/>.
    /// </summary>
    public class ProjectionModificationOptions
    {
        /// <param name="missingProjectionBehavior">Behavior when the projection does not exists.</param>
        /// <param name="existingProjectionBehavior">Behavior when the projection already exists.</param>
        public ProjectionModificationOptions(
            MissingProjectionModificationBehavior missingProjectionBehavior,
            ExistingProjectionModificationBehavior existingProjectionBehavior)
        {
            MissingProjectionBehavior = missingProjectionBehavior;
            ExistingProjectionBehavior = existingProjectionBehavior;
        }

        /// <summary>
        /// Behavior when the projection does not exists.
        /// </summary>
        public MissingProjectionModificationBehavior MissingProjectionBehavior { get; }

        /// <summary>
        /// Behavior when the projection already exists.
        /// </summary>
        public ExistingProjectionModificationBehavior ExistingProjectionBehavior { get; }
    }

    /// <summary>
    /// Specifies behavior for <see cref="ProjectionModificationHandler{TProjection,TKey,TContext}"/>
    /// when the projection does not exists.
    /// </summary>
    public enum MissingProjectionModificationBehavior
    {
        /// <summary>
        /// Creates a new projection when the projection does not exists.
        /// </summary>
        Create,

        /// <summary>
        /// Does nothing when the projection does not exists.
        /// </summary>
        Ignore,

        /// <summary>
        /// Throws an exception when the projection does not exists.
        /// </summary>
        Throw
    }

    /// <summary>
    /// Specifies behavior for <see cref="ProjectionModificationHandler{TProjection,TKey,TContext}"/>
    /// when the projection already exists.
    /// </summary>
    public enum ExistingProjectionModificationBehavior
    {
        /// <summary>
        /// Updates the projection when it already exists.
        /// </summary>
        Update,

        /// <summary>
        /// Does nothing when the projection already exists.
        /// </summary>
        Ignore,

        /// <summary>
        /// Throws an exception when the projection already exists.
        /// </summary>
        Throw
    }

    /// <summary>
    /// Handles projection deletion asynchronously using context <see cref="IEventMap{TContext}"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the projection keys.</typeparam>
    /// <typeparam name="TContext">The type of the context.</typeparam>
    /// <param name="key">The key of the projection.</param>
    /// <param name="context">The context.</param>
    /// <param name="options">Additional options <see cref="ProjectionDeletionOptions"/>.</param>
    public delegate Task ProjectionDeletionHandler<in TKey, in TContext>(
        TKey key,
        TContext context,
        ProjectionDeletionOptions options);

    /// <summary>
    /// Provides additional options for <see cref="ProjectionDeletionHandler{TKey,TContext}"/>.
    /// </summary>
    public class ProjectionDeletionOptions
    {
        /// <param name="missingProjectionBehavior">Behavior when the projection does not exists.</param>
        public ProjectionDeletionOptions(MissingProjectionDeletionBehavior missingProjectionBehavior)
        {
            MissingProjectionBehavior = missingProjectionBehavior;
        }

        /// <summary>
        /// Behavior when the projection does not exists.
        /// </summary>
        public MissingProjectionDeletionBehavior MissingProjectionBehavior { get; }
    }

    /// <summary>
    /// Specifies behavior for <see cref="ProjectionDeletionHandler{TKey,TContext}"/>
    /// when the projection does not exists.
    /// </summary>
    public enum MissingProjectionDeletionBehavior
    {
        /// <summary>
        /// Does nothing when the projection does not exists.
        /// </summary>
        Ignore,

        /// <summary>
        /// Throws an exception when the projection does not exists.
        /// </summary>
        Throw
    }
}