using System;
using System.Threading.Tasks;

namespace LiquidProjections.MapBuilding
{
    /// <summary>
    /// Allows to configure event map how to handle projection creation
    /// for events of type <typeparamref name="TEvent"/> and projections of type <typeparamref name="TProjection"/>
    /// using context <typeparamref name="TContext"/>
    /// if the projection does not exist yet.
    /// </summary>
    public interface ICreateIfDoesNotExistEventActionBuilder<TEvent, TProjection, out TContext>
    {
        /// <summary>
        /// Finishes configuring a projection creation handler for projections which do not exist yet.
        /// </summary>
        /// <param name="projector">
        /// The asynchronous delegate that initializes the created projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        void Using(Func<TProjection, TEvent, TContext, Task> projector);
    }
}