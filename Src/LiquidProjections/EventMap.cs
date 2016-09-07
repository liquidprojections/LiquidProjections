using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Allows mapping events to updates, deletes or custom actions on (existing) projections in a fluent fashion. 
    /// </summary>
    public class EventMap<TProjection, TContext> : IEventMap<TProjection, TContext>
    {
        private readonly Dictionary<Type, List<GetHandlerFor>> mappings = new Dictionary<Type, List<GetHandlerFor>>();

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
            var handler = mappings.ContainsKey(key) ? mappings[key].FirstOrDefault() : null;

            return handler?.Invoke(@event);
        }

        private void Add<TEvent>(Func<TEvent, TContext, Task> action)
        {
            if (!mappings.ContainsKey(typeof(TEvent)))
            {
                mappings[typeof(TEvent)] = new List<GetHandlerFor>();
            }

            mappings[typeof(TEvent)].Add(@event => new Handler<TEvent>(action).GetHandler(@event));
        }

        public class Action<TEvent>
        {
            private readonly EventMap<TProjection, TContext> parent;
            private readonly List<Func<TEvent, bool>> predicates = new List<Func<TEvent, bool>>();

            internal Action(EventMap<TProjection, TContext> parent)
            {
                this.parent = parent;
            }

            public Action<TEvent> Where(Func<TEvent, bool> predicate)
            {
                predicates.Add(predicate);
                return this;
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
                Add((@event, ctx) => parent.updateHandler(selector(@event), ctx,
                    (projection, innerCtx) => projector(projection, @event, innerCtx)));
            }

            public void AsDeleteOf(Func<TEvent, string> selector)
            {
                Add((@event, ctx) => parent.deleteHandler(selector(@event), ctx));
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                Add((@event, ctx) => parent.customHandler(ctx, innerCtx => action(@event, innerCtx)));
            }

            private void Add(Func<TEvent, TContext, Task> action)
            {
                parent.Add<TEvent>((@event, ctx) =>
                {
                    return predicates.All(p => p(@event)) ? action(@event, ctx) : Task.FromResult(0);
                });
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