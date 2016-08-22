using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Represents a simple map between events and actions which can be used by projectors.
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class EventMap<TContext> where TContext : ProjectionContext
    {
        private readonly IDictionary<Type, IHandler> mappings = new Dictionary<Type, IHandler>();


        /// <summary>
        /// Maps <typeparamref name="TEvent"/> to an action.
        /// </summary>
        public EventAction<TEvent> Map<TEvent>()
        {
            return new EventAction<TEvent>(this);
        }

        /// <summary>
        /// Gets an asynchronous handler for <paramref name="event"/> or <c>null</c> if no handler
        /// has been registered.
        /// </summary>
        public Func<TContext, Task> GetHandler(object @event)
        {
            Type key = @event.GetType();
            return mappings.ContainsKey(key) ? mappings[key].GetHandler(@event) : null;
        }

        private void Add(Type type, IHandler handler)
        {
            mappings.Add(type, handler);
        }

        public class EventAction<TEvent>
        {
            private readonly EventMap<TContext> parent;

            public EventAction(EventMap<TContext> parent)
            {
                this.parent = parent;
            }

            public void As(Action<TEvent, TContext> projector)
            {
                As((e, ctx) =>
                {
                    projector(e, ctx);

                    return Task.FromResult(0);
                });
            }

            public void As(Func<TEvent, TContext, Task> projector)
            {
                parent.Add(typeof(TEvent), new Handler<TEvent>(projector));
            }
        }
        
        private interface IHandler
        {
            Func<TContext, Task> GetHandler(object @event);
        }

        private class Handler<TEvent> : IHandler
        {
            private readonly Func<TEvent, TContext, Task> projector;

            public Handler(Func<TEvent, TContext, Task> projector)
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