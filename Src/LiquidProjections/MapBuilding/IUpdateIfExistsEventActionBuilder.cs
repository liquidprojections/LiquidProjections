using System;
using System.Threading.Tasks;

namespace LiquidProjections.MapBuilding
{
    /// <summary>
    /// Allows to configure event map how to handle projection updating
    /// for events of type <typeparamref name="TEvent"/> and projections of type <typeparamref name="TProjection"/>
    /// using context <typeparamref name="TContext"/>.
    /// </summary>
    public interface IUpdateIfExistsEventActionBuilder<TEvent, TProjection, out TContext>
    {
        /// <summary>
        /// Finishes configuring a projection updating handler for projections which do already exist.
        /// </summary>
        /// <param name="projector">
        /// The asynchronous delegate that updates the projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        void Using(Func<TProjection, TEvent, TContext, Task> projector);
    }
}