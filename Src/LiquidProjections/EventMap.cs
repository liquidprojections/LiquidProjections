using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class EventMap<TProjection, TKey, TContext> : IEventMap<TContext>
    {
        private readonly Dictionary<Type, List<GetHandlerFor>> mappings = new Dictionary<Type, List<GetHandlerFor>>();

        internal UpdateHandler<TKey, TContext, TProjection> Update { get; set; }

        internal DeleteHandler<TKey, TContext> Delete { get; set; }

        internal CustomHandler<TContext> Do { get; set; }

        internal void Add<TEvent>(Func<TEvent, TContext, Task> action)
        {
            if (!mappings.ContainsKey(typeof(TEvent)))
            {
                mappings[typeof(TEvent)] = new List<GetHandlerFor>();
            }

            mappings[typeof(TEvent)].Add(@event => new HandlerWrapper<TEvent>(action).GetHandler(@event));
        }

        /// <summary>
        /// Gets an asynchronous handler for <paramref name="event"/> or <c>null</c> if no handler
        /// has been registered.
        /// </summary>
        public Func<TContext, Task> GetHandler(object @event)
        {
            Type key = @event.GetType();
            GetHandlerFor[] handlerWrappers = mappings.ContainsKey(key) ? mappings[key].ToArray() : new GetHandlerFor[0];

            return async ctx =>
            {
                foreach (GetHandlerFor wrapper in handlerWrappers)
                {
                    Func<TContext, Task> handler = wrapper(@event);
                    await handler(ctx);
                }
            };
        }

        private delegate Func<TContext, Task> GetHandlerFor(object @event);

        private class HandlerWrapper<TEvent>
        {
            private readonly Func<TEvent, TContext, Task> projector;

            public HandlerWrapper(Func<TEvent, TContext, Task> projector)
            {
                this.projector = projector;
            }

            public Func<TContext, Task> GetHandler(object @event)
            {
                return ctx => projector((TEvent)@event, ctx);
            }
        }
    }
}