using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Routes events to their configured handlers using context <typeparamref name="TContext"/>.
    /// </summary>
    public class EventMap<TContext> : IEventMap<TContext>
    {
        private readonly Dictionary<Type, List<Handler>> mappings = new Dictionary<Type, List<Handler>>();

        internal CustomHandler<TContext> Do { get; set; }

        internal void Add<TEvent>(Func<TEvent, TContext, Task> action)
        {
            if (!mappings.ContainsKey(typeof(TEvent)))
            {
                mappings[typeof(TEvent)] = new List<Handler>();
            }

            mappings[typeof(TEvent)].Add((@event, context) => action((TEvent)@event, context));
        }

        /// <summary>
        /// Handles <paramref name="anEvent"/> asynchronously using context <paramref name="context"/>.
        /// </summary>
        public async Task<bool> Handle(object anEvent, TContext context)
        {
            if (anEvent == null)
            {
                throw new ArgumentNullException(nameof(anEvent));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Type key = anEvent.GetType();

            if (mappings.TryGetValue(key, out var handlers))
            {
                foreach (Handler handler in handlers)
                {
                    await handler(anEvent, context);
                }

                return true;
            }

            return false;
        }

        private delegate Task Handler(object @event, TContext context);
    }
}