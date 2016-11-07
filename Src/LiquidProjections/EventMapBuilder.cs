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
        private readonly EventMap<TProjection, TKey, TContext> eventMap;
        private readonly EventMapBuilder<TContext> innerBuilder;

        public EventMapBuilder()
        {
            eventMap = new EventMap<TProjection, TKey, TContext>();
            innerBuilder = new EventMapBuilder<TContext>(eventMap);
        }

        public EventMappingBuilder<TEvent> Map<TEvent>()
        {
            return new EventMappingBuilder<TEvent>(eventMap);
        }

        public void HandleCreatesAs(CreateHandler<TKey, TContext, TProjection> handler)
        {
            eventMap.Create = handler;
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
            innerBuilder.HandleCustomActionsAs(handler);
        }

        public class EventMappingBuilder<TEvent>
        {
            private readonly EventMap<TProjection, TKey, TContext> eventMap;
            private readonly EventMapBuilder<TContext>.EventMappingBuilder<TEvent> innerBuilder;

            public EventMappingBuilder(EventMap<TProjection, TKey, TContext> eventMap)
            {
                this.eventMap = eventMap;
                innerBuilder = new EventMapBuilder<TContext>.EventMappingBuilder<TEvent>(eventMap);
            }

            public EventMappingBuilder<TEvent> When(Func<TEvent, bool> predicate)
            {
                innerBuilder.When(predicate);
                return this;
            }

            public CreateActionBuilder<TEvent> AsCreateOf(Func<TEvent, TKey> selector)
            {
                return new CreateActionBuilder<TEvent>(projector =>
                {
                    innerBuilder.Add((@event, ctx) => eventMap.Create(selector(@event), ctx,
                        (projection, innerCtx) => projector(projection, @event, innerCtx)));
                });
            }

            public UpdateActionBuilder<TEvent> AsUpdateOf(Func<TEvent, TKey> selector)
            {
                return new UpdateActionBuilder<TEvent>(projector =>
                {
                    innerBuilder.Add((@event, ctx) => eventMap.Update(selector(@event), ctx,
                        (projection, innerCtx) => projector(projection, @event, innerCtx)));
                });
            }

            public void AsDeleteOf(Func<TEvent, TKey> selector)
            {
                innerBuilder.Add((@event, ctx) => eventMap.Delete(selector(@event), ctx));
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                innerBuilder.As(action);
            }

            public void As(Action<TEvent, TContext> action)
            {
                innerBuilder.As(action);
            }
        }

        public class CreateActionBuilder<TEvent>
        {
            private readonly Action<Func<TProjection, TEvent, TContext, Task>> action;

            public CreateActionBuilder(Action<Func<TProjection, TEvent, TContext, Task>> action)
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

        public IEventMap<TContext> Build()
        {
            return innerBuilder.Build();
        }
    }
    
    /// <summary>
    /// Allows mapping events to custom actions in a fluent fashion. 
    /// </summary>
    public class EventMapBuilder<TContext> : IEventMapBuilder<TContext>
    {
        private readonly EventMap<TContext> eventMap;

        public EventMapBuilder() : this(new EventMap<TContext>())
        {
        }

        public EventMapBuilder(EventMap<TContext> eventMap)
        {
            this.eventMap = eventMap;
        }

        public EventMappingBuilder<TEvent> Map<TEvent>()
        {
            return new EventMappingBuilder<TEvent>(eventMap);
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
            private readonly EventMap<TContext> eventMap;
            private readonly List<Func<TEvent, bool>> predicates = new List<Func<TEvent, bool>>();

            public EventMappingBuilder(EventMap<TContext> eventMap)
            {
                this.eventMap = eventMap;
            }

            public EventMappingBuilder<TEvent> When(Func<TEvent, bool> predicate)
            {
                predicates.Add(predicate);
                return this;
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                Add((@event, ctx) => eventMap.Do(ctx, innerCtx => action(@event, innerCtx)));
            }

            public void As(Action<TEvent, TContext> action)
            {
                As((anEvent, context) =>
                {
                    action(anEvent, context);
                    return Task.FromResult(0);
                });
            }

            internal void Add(Func<TEvent, TContext, Task> action)
            {
                eventMap.Add<TEvent>((@event, ctx) =>
                {
                    return predicates.All(p => p(@event)) ? action(@event, ctx) : Task.FromResult(0);
                });
            }
        }
    }
}
