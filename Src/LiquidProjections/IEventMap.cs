using System;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Defines the contract between event maps and projection storage providers.
    /// </summary>
    /// <typeparam name="TContext">
    /// An object that provides additional information and metadata to the consuming projection code.
    /// </typeparam>
    public interface IEventMap<in TContext>
    {
        /// <summary>
        /// Gets an asynchronous handler for <paramref name="event"/> or <c>null</c> if no handler
        /// has been registered.
        /// </summary>
        Func<TContext, Task> GetHandler(object @event);
    }
}