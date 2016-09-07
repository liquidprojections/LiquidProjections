using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Allows mapping events to updates, deletes or custom actions on (existing) projections in a fluent fashion. 
    /// </summary>
    public class EventMap<TProjection, TContext> : IEventMap<TProjection, TContext>
    {
        private readonly IDictionary<Type, GetHandlerFor> mappings = new Dictionary<Type, GetHandlerFor>();

        private UpdateHandler<TContext, TProjection> updateHandler = null;
        private DeleteHandler<TContext> deleteHandler = null;
        private CustomHandler<TContext> customHandler = null;

        public Action<TEvent> Map<TEvent>()
        {
            return new Action<TEvent>(this);
        }

        public void ForwardUpdatesTo(UpdateHandler<TContext, TProjection> handler)
        {
            updateHandler = handler;
        }

        public void ForwardDeletesTo(DeleteHandler<TContext> handler)
        {
            deleteHandler = handler;
        }
        public void ForwardCustomActionsTo(CustomHandler<TContext> handler)
        {
            customHandler = handler;
        }

        /// <summary>
        /// Gets an asynchronous handler for <paramref name="event"/> or <c>null</c> if no handler
        /// has been registered.
        /// </summary>
        public Func<TContext, Task> GetHandler(object @event)
        {
            Type key = @event.GetType();
            return mappings.ContainsKey(key) ? mappings[key](@event) : null;
        }

        private void Add<TEvent>(Func<TEvent, TContext, Task> action)
        {
            mappings.Add(typeof(TEvent), @event => new Handler<TEvent>(action).GetHandler(@event));
        }

        public class Action<TEvent>
        {
            private readonly EventMap<TProjection, TContext> parent;

            internal Action(EventMap<TProjection, TContext> parent)
            {
                this.parent = parent;
            }

            public void AsUpdateOf(Func<TEvent, string> selector, Action<TProjection, TEvent, TContext> projector)
            {
                AsUpdateOf(selector, (p, e, ctx) =>
                {
                    projector(p, e, ctx);
                    return Task.FromResult(0);
                });
            }

            public void AsUpdateOf(Func<TEvent, string> selector, Func<TProjection, TEvent, TContext, Task> projector)
            {
                parent.Add<TEvent>((@event, ctx) =>
                        parent.updateHandler(selector(@event), ctx, (projection, innerCtx) => projector(projection, @event, innerCtx)));
            }

            public void AsDeleteOf(Func<TEvent, string> selector)
            {
                parent.Add<TEvent>((@event, ctx) => parent.deleteHandler(selector(@event), ctx));
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                parent.Add<TEvent>((@event, ctx) => parent.customHandler(ctx, innerCtx => action(@event, innerCtx)));
            }
        }

        private delegate Func<TContext, Task> GetHandlerFor(object @event);

        private class Handler<TEvent>
        {
            private readonly Func<TEvent, TContext, Task> projector;

            public Handler(Func<TEvent, TContext, Task> projector)
            {
                this.projector = projector;
            }

            public Func<TContext, Task> GetHandler(object @event)
            {
                return ctx => projector((TEvent) @event, ctx);
            }
        }
    }
}