using System;
using System.Threading.Tasks;

namespace LiquidProjections.MapBuilding
{
    /// <summary>
    /// Allows to configure event map how to handle projection updating
    /// for events of type <typeparamref name="TEvent"/> and projections of type <typeparamref name="TProjection"/>
    /// using context <typeparamref name="TContext"/>.
    /// </summary>
    public interface IUpdateAction<out TEvent, TKey, out TProjection, out TContext>
    {
        /// <summary>
        /// Finishes configuring a projection updating handler.
        /// </summary>
        /// <param name="updateAction">
        /// The asynchronous delegate that updates the projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        IUpdateAction<TEvent, TKey, TProjection, TContext> Using(Func<TProjection, TEvent, TContext, Task> updateAction);

        /// <summary>
        /// Configures the update action to throw a <see cref="ProjectionException"/> if the projection was missing.
        /// </summary>
        IUpdateAction<TEvent, TKey, TProjection, TContext> ThrowingIfMissing();

        /// <summary>
        /// Configures the update action to ignore a missing projection and continue with the next event.
        /// </summary>
        IUpdateAction<TEvent, TKey, TProjection, TContext> IgnoringMisses();

        /// <summary>
        /// Configures the update action to create the projection if it is missing.
        /// </summary>
        IUpdateAction<TEvent, TKey, TProjection, TContext> CreatingIfMissing();

        /// <summary>
        /// Allows some custom code to be executed when the a projection was missing and to optionally tell the projector
        /// to create it.
        /// </summary>
        IUpdateAction<TEvent, TKey, TProjection, TContext> HandlingMissesUsing(Func<TKey, TContext, bool> action);
    }
}