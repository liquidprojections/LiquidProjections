using System;

namespace LiquidProjections.MapBuilding
{
    public interface IDeleteAction<TEvent, out TKey, out TContext>
    {
        /// <summary>
        /// Causes the mapping to throw if deleting the projection failed or the projection did not exist anymore.
        /// </summary>
        IDeleteAction<TEvent, TKey, TContext> ThrowingIfMissing();

        /// <summary>
        /// Configures the mapping to ignore any failed attempts to delete a projection. 
        /// </summary>
        IDeleteAction<TEvent, TKey, TContext> IgnoringMisses();

        /// <summary>
        /// Allows the consumer to handle missing projections in a custom way. 
        /// </summary>
        IDeleteAction<TEvent, TKey, TContext> HandlingMissesUsing(Action<TKey, TContext> action);
    }
}