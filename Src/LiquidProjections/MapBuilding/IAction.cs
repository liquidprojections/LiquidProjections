using System;
using System.Threading.Tasks;

namespace LiquidProjections.MapBuilding
{
    /// <summary>
    /// Allows to configure event map how to handle custom actions for events of type <typeparamref name="TEvent"/>
    /// using context <typeparamref name="TContext"/>.
    /// </summary>
    public interface IAction<TEvent, out TContext>
    {
        /// <summary>
        /// Finishes configuring a custom handler for events of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <param name="action">
        /// The asynchronous delegate that handles the event.
        /// Takes the event and the context as the parameters.
        /// </param>
        void As(Func<TEvent, TContext, Task> action);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="predicate">
        /// The asynchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event and the context as the parameters.
        /// </param>
        /// <returns>
        /// <see cref="IAction{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        IAction<TEvent, TContext> When(Func<TEvent, TContext, Task<bool>> predicate);
    }

    /// <summary>
    /// Allows to configure event map how to handle custom actions, projection creation, updating and deletion
    /// for events of type <typeparamref name="TEvent"/> and projections of type <typeparamref name="TProjection"/>
    /// with key of type <typeparamref name="TKey"/>
    /// using context <typeparamref name="TContext"/>.
    /// </summary>
    public interface ICrudAction<TEvent, TProjection, TKey, TContext> : IAction<TEvent, TContext>
    {
        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that a new projection with the specified key will be created for the event handler.
        /// An exception will be thrown if a projection with such key already exists.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="ICreateAction{TEvent,TProjection,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        ICreateAction<TEvent, TProjection, TContext> AsCreateOf(Func<TEvent, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that a new projection with the specified key will be created for the event handler.
        /// An exception will be thrown if a projection with such key already exists.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="ICreateAction{TEvent,TProjection,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        ICreateAction<TEvent, TProjection, TContext> AsCreateOf(Func<TEvent, TContext, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that a new projection with the specified key will be created for the event handler.
        /// The event will not be handled by the handler if a projection with such key already exists.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="ICreateIfDoesNotExistEventActionBuilder{TEvent,TProjection,TContext}"/>
        /// that allows to continue configuring the handler.
        /// </returns>
        [Obsolete("Use AsCreateOf().IgnoringDuplicates() instead")]
        ICreateAction<TEvent, TProjection, TContext> AsCreateIfDoesNotExistOf(Func<TEvent, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that a new projection with the specified key will be created for the event handler.
        /// The event will not be handled by the handler if a projection with such key already exists.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="ICreateIfDoesNotExistEventActionBuilder{TEvent,TProjection,TContext}"/>
        /// that allows to continue configuring the handler.
        /// </returns>
        [Obsolete("Use AsCreateOf().IgnoringDuplicates() instead")]
        ICreateAction<TEvent, TProjection, TContext> AsCreateIfDoesNotExistOf(Func<TEvent, TContext, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be updated by the event handler.
        /// An exception will be thrown if a projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="IUpdateAction{TEvent,TKey, TProjection,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        IUpdateAction<TEvent, TKey, TProjection, TContext> AsUpdateOf(Func<TEvent, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be updated by the event handler.
        /// An exception will be thrown if a projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="IUpdateAction{TEvent,TKey, TProjection,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        IUpdateAction<TEvent, TKey, TProjection, TContext> AsUpdateOf(Func<TEvent, TContext, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be updated by the event handler.
        /// The event will not be handled by the handler if the projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="IUpdateAction{TEvent,TKey, TProjection,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        [Obsolete("Use AsUpdateOf().IgnoringMissing() instead")]
        IUpdateAction<TEvent, TKey, TProjection, TContext> AsUpdateIfExistsOf(Func<TEvent, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be updated by the event handler.
        /// The event will not be handled by the handler if the projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="IUpdateAction{TEvent,TKey, TProjection,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        [Obsolete("Use AsUpdateOf().IgnoringMissing() instead")]
        IUpdateAction<TEvent, TKey, TProjection, TContext> AsUpdateIfExistsOf(Func<TEvent, TContext, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that a new projection with the specified key will be created for the event handler.
        /// If a projection with such key already exists, it will be updated by the event handler instead.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="ICreateOrUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>
        /// that allows to continue configuring the handler.
        /// </returns>
        [Obsolete("Use AsCreateOf().OverwritingDuplicates() instead")]
        ICreateAction<TEvent, TProjection, TContext> AsCreateOrUpdateOf(Func<TEvent, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that a new projection with the specified key will be created for the event handler.
        /// If a projection with such key already exists, it will be updated by the event handler instead.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        /// <returns>
        /// <see cref="ICreateOrUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>
        /// that allows to continue configuring the handler.
        /// </returns>
        [Obsolete("Use AsCreateOf().OverwritingDuplicates() instead")]
        ICreateAction<TEvent, TProjection, TContext> AsCreateOrUpdateOf(Func<TEvent, TContext, TKey> getKey);

        /// <summary>
        /// Finishes configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be deleted when handling the event.
        /// An exception will be thrown if a projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        IDeleteAction<TEvent, TKey, TContext> AsDeleteOf(Func<TEvent, TKey> getKey);

        /// <summary>
        /// Finishes configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be deleted when handling the event.
        /// An exception will be thrown if a projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        IDeleteAction<TEvent, TKey, TContext> AsDeleteOf(Func<TEvent, TContext, TKey> getKey);

        /// <summary>
        /// Finishes configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be deleted when handling the event.
        /// The event will not be handled if the projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        [Obsolete("Use AsDeleteOf().IgnoringMissing() instead")]
        IDeleteAction<TEvent, TKey, TContext> AsDeleteIfExistsOf(Func<TEvent, TKey> getKey);

        /// <summary>
        /// Finishes configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Specifies that the projection with the specified key will be deleted when handling the event.
        /// The event will not be handled if the projection with such key does not exist.
        /// </summary>
        /// <param name="getKey">The delegate that determines the projection key for the event.</param>
        [Obsolete("Use AsDeleteOf().IgnoringMissing() instead")]
        IDeleteAction<TEvent, TKey, TContext> AsDeleteIfExistsOf(Func<TEvent, TContext, TKey> getKey);

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="predicate">
        /// The asynchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event and the context as the parameters.
        /// </param>
        /// <returns>
        /// <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        new ICrudAction<TEvent, TProjection, TKey, TContext> When(Func<TEvent, TContext, Task<bool>> predicate);
    }
}
