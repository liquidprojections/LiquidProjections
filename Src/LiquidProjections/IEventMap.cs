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
        /// Handles <paramref name="anEvent"/> asynchronously.
        /// </summary>
        /// <remarks>
        /// Returns a value indicating whether the event was handled by any event.
        /// </remarks>
        Task<bool> Handle(object anEvent, TContext context);
    }
}