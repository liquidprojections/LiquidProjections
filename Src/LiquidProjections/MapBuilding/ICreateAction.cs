using System;
using System.Threading.Tasks;

namespace LiquidProjections.MapBuilding
{
    /// <summary>
    /// Allows to configure event map how to handle projection creation
    /// for events of type <typeparamref name="TEvent"/> and projections of type <typeparamref name="TProjection"/>
    /// using context <typeparamref name="TContext"/>.
    /// </summary>
    public interface ICreateAction<out TEvent, out TProjection, out TContext>
    {
        /// <summary>
        /// Finishes configuring a projection creation handler.
        /// </summary>
        /// <param name="projector">
        /// The asynchronous delegate that initializes the created projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        ICreateAction<TEvent, TProjection, TContext> Using(Func<TProjection, TEvent, TContext, Task> projector);

        /// <summary>
        /// Tells the implementing projector that duplicates should be ignored.
        /// </summary>
        ICreateAction<TEvent, TProjection, TContext> IgnoringDuplicates();

        /// <summary>
        /// Configures the action to overwrite any duplicates.
        /// </summary>
        ICreateAction<TEvent, TProjection, TContext> OverwritingDuplicates();

        /// <summary>
        /// Allows custom handling of duplicates found while trying to create a new projection.
        /// </summary>
        /// <param name="shouldOverwrite">
        /// A predicate that allows the handler to decide whether or not to overwrite the duplicate projection.  
        /// </param>
        ICreateAction<TEvent, TProjection, TContext> HandlingDuplicatesUsing(Func<TProjection, TEvent, TContext, bool> shouldOverwrite);
    }
}