using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LiquidProjections
{
    /// <summary>
    /// Allows mapping events to updates, deletes or custom actions on (existing) projections in a fluent fashion. 
    /// </summary>
    public class EventMapBuilder<TProjection, TKey, TContext> : IEventMapBuilder<TProjection, TKey, TContext>
    {
        private readonly EventMap<TProjection, TKey, TContext> eventMap = new EventMap<TProjection, TKey, TContext>();

        public EventMappingBuilder<TEvent> Map<TEvent>()
        {
            return new EventMappingBuilder<TEvent>(eventMap);
        }

        public void HandleUpdatesAs(UpdateHandler<TKey, TContext, TProjection> handler)
        {
            eventMap.Update = handler;
        }

        public void HandleDeletesAs(DeleteHandler<TKey, TContext> handler)
        {
            eventMap.Delete = handler;
        }

        public void HandleCustomActionsAs(CustomHandler<TContext> handler)
        {
            eventMap.Do = handler;
        }

        public IEventMap<TContext> Build()
        {
            return eventMap;
        }

        public class EventMappingBuilder<TEvent>
        {
            private readonly EventMap<TProjection, TKey, TContext> eventMap;
            private readonly List<Func<TEvent, bool>> predicates = new List<Func<TEvent, bool>>();

            public EventMappingBuilder(EventMap<TProjection, TKey, TContext> eventMap)
            {
                this.eventMap = eventMap;
            }

            public EventMappingBuilder<TEvent> When(Func<TEvent, bool> predicate)
            {
                predicates.Add(predicate);
                return this;
            }

            public UpdateActionBuilder<TEvent> AsUpdateOf(Func<TEvent, TKey> selector)
            {
                return new UpdateActionBuilder<TEvent>(projector =>
                {
                    Add((@event, ctx) => eventMap.Update(selector(@event), ctx,
                        (projection, innerCtx) => projector(projection, @event, innerCtx)));
                });
            }

            public void AsDeleteOf(Func<TEvent, TKey> selector)
            {
                Add((@event, ctx) => eventMap.Delete(selector(@event), ctx));
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                Add((@event, ctx) => eventMap.Do(ctx, innerCtx => action(@event, innerCtx)));
            }

            private void Add(Func<TEvent, TContext, Task> action)
            {
                eventMap.Add<TEvent>((@event, ctx) =>
                {
                    return predicates.All(p => p(@event)) ? action(@event, ctx) : Task.FromResult(0);
                });
            }
        }

        public class UpdateActionBuilder<TEvent>
        {
            private readonly Action<Func<TProjection, TEvent, TContext, Task>> action;

            public UpdateActionBuilder(Action<Func<TProjection, TEvent, TContext, Task>> action)
            {
                this.action = action;
            }

            public void Using(Action<TProjection, TEvent, TContext> projector)
            {
                Using((p, e, ctx) =>
                {
                    projector(p, e, ctx);
                    return Task.FromResult(0);
                });
            }

            public void Using(Func<TProjection, TEvent, TContext, Task> projector)
            {
                action(projector);
            }
        }
    }
}