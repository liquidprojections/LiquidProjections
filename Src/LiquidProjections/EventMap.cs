using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Routes events to their configured handlers using context <typeparamref name="TContext"/>.
    /// </summary>
    public class EventMap<TContext> : IEventMap<TContext>
    {
        private readonly Dictionary<Type, List<Handler>> mappings = new Dictionary<Type, List<Handler>>();
        private readonly List<Func<object, TContext, Task<bool>>> filters =
            new List<Func<object, TContext, Task<bool>>>();

        internal void Add<TEvent>(Func<TEvent, TContext, Task> action)
        {
            if (!mappings.ContainsKey(typeof(TEvent)))
            {
                mappings[typeof(TEvent)] = new List<Handler>();
            }

            mappings[typeof(TEvent)].Add((@event, context) => action((TEvent)@event, context));
        }

        internal void AddFilter(Func<object, TContext, Task<bool>> filter)
        {
            filters.Add(filter);
        }

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

            if (await PassesFilter(anEvent, context))
            {
                Type key = anEvent.GetType();

                if (mappings.TryGetValue(key, out var handlers))
                {
                    foreach (Handler handler in handlers)
                    {
                        await handler(anEvent, context);
                    }

                    return true;
                }
            }

            return false;
        }

        private async Task<bool> PassesFilter(object anEvent, TContext context)
        {
            if (filters.Count > 0)
            {
                bool[] results = await Task.WhenAll(filters.Select(filter => filter(anEvent, context)));

                return results.All(x => x);
            }
            else
            {
                return true;
            }
        }

        private delegate Task Handler(object @event, TContext context);
    }
}