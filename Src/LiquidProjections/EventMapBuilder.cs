using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LiquidProjections.MapBuilding;

namespace LiquidProjections
{
    /// <summary>
    /// Allows mapping events to handlers in a fluent fashion using context <typeparamref name="TContext"/>. 
    /// </summary>
    public sealed class EventMapBuilder<TContext> : IEventMapBuilder<TContext>
    {
        private readonly EventMap<TContext> eventMap = new EventMap<TContext>();
        private ProjectorMap<TContext> projector;

        /// <summary>
        /// Ensures that only events matching the predicate are processed. 
        /// </summary>
        public EventMapBuilder<TContext> Where(Func<object, TContext, Task<bool>> filter)
        {
            eventMap.AddFilter(filter);
            return this;
        }

        /// <summary>
        /// Starts configuring a new handler for events of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <returns>
        /// <see cref="IAction{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public IAction<TEvent, TContext> Map<TEvent>()
        {
            AssertNotBuilt();

            return new Action<TEvent>(this, () => projector);
        }

        /// <summary>
        /// Builds the resulting event map. 
        /// </summary>
        /// <remarks>
        /// Can only be called once.
        /// No changes can be made after the event map has been built.
        /// </remarks>
        /// <param name="projector">
        /// Contains the handler that a projector needs to support to handle events from this map. 
        /// </param>
        public IEventMap<TContext> Build(ProjectorMap<TContext> projector)
        {
            AssertNotBuilt();

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            if (projector.Custom == null)
            {
                throw new ArgumentException(
                    $"Expected the Custom property to point to a valid instance of {nameof(CustomHandler<TContext>)}", nameof(projector));
            }

            this.projector = projector;

            return eventMap;
        }

        private void AssertNotBuilt()
        {
            if (projector != null)
            {
                throw new InvalidOperationException("The event map has already been built.");
            }
        }

        private sealed class Action<TEvent> : IAction<TEvent, TContext>
        {
            private readonly EventMapBuilder<TContext> parent;
            private readonly Func<ProjectorMap<TContext>> getProjector;

            private readonly List<Func<TEvent, TContext, Task<bool>>> predicates =
                new List<Func<TEvent, TContext, Task<bool>>>();

            public Action(EventMapBuilder<TContext> parent, Func<ProjectorMap<TContext>> getProjector)
            {
                this.parent = parent;
                this.getProjector = getProjector;
            }

            public IAction<TEvent, TContext> When(Func<TEvent, TContext, Task<bool>> predicate)
            {
                if (predicate == null)
                {
                    throw new ArgumentNullException(nameof(predicate));
                }

                predicates.Add(predicate);
                return this;
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                if (action == null)
                {
                    throw new ArgumentNullException(nameof(action));
                }

                Add((anEvent, context) => getProjector().Custom(context, async () => await action(anEvent, context)));
            }

            private void Add(Func<TEvent, TContext, Task> action)
            {
                parent.eventMap.Add<TEvent>(async (anEvent, context) =>
                {
                    foreach (Func<TEvent, TContext, Task<bool>> predicate in predicates)
                    {
                        if (!await predicate(anEvent, context))
                        {
                            return;
                        }
                    }

                    await action(anEvent, context);
                });
            }
        }
    }

    /// <summary>
    /// Allows mapping events to handlers in a fluent fashion using context <typeparamref name="TContext"/>.
    /// Takes care of loading, creating, updating and deleting of projections of type <typeparamref name="TProjection"/>
    /// with key of type <typeparamref name="TProjection"/>.
    /// </summary>
    public sealed class EventMapBuilder<TProjection, TKey, TContext> : IEventMapBuilder<TProjection, TKey, TContext>
    {
        private readonly EventMapBuilder<TContext> innerBuilder = new EventMapBuilder<TContext>();
        private ProjectorMap<TProjection, TKey, TContext> projector;

        
        /// <summary>
        /// Ensures that only events matching the predicate are processed. 
        /// </summary>
        public EventMapBuilder<TProjection, TKey, TContext> Where(Func<object, TContext, Task<bool>> predicate)
        {
            innerBuilder.Where(predicate);
            return this;
        }
        
        /// <summary>
        /// Starts configuring a new handler for events of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <returns>
        /// <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public ICrudAction<TEvent, TProjection, TKey, TContext> Map<TEvent>()
        {
            return new CrudAction<TEvent>(this);
        }

        /// <summary>
        /// Builds the resulting event map. 
        /// </summary>
        /// <remarks>
        /// Can only be called once.
        /// No changes can be made after the event map has been built.
        /// </remarks>
        /// <param name="projector">
        /// Contains the create, update, delete and custom handlers that a projector needs to support to handle events from this map. 
        /// </param>
        public IEventMap<TContext> Build(ProjectorMap<TProjection, TKey, TContext> projector)
        {
            this.projector = projector;
            return innerBuilder.Build(new ProjectorMap<TContext>
            {
                Custom = (context, projectEvent) => projectEvent()
            });
        }

        private sealed class CrudAction<TEvent> : ICrudAction<TEvent, TProjection, TKey, TContext>
        {
            private readonly IAction<TEvent, TContext> actionBuilder;
            private readonly Func<ProjectorMap<TProjection, TKey, TContext>> getProjector;

            public CrudAction(EventMapBuilder<TProjection, TKey, TContext> parent)
            {
                actionBuilder = parent.innerBuilder.Map<TEvent>();
                getProjector = () => parent.projector;
            }

            public ICreateAction<TEvent, TProjection, TContext> AsCreateOf(Func<TEvent, TContext, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new CreateAction(actionBuilder, getProjector, getKey);
            }

            public ICreateAction<TEvent, TProjection, TContext> AsCreateOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new CreateAction(actionBuilder, getProjector, getKey);
            }

            public ICreateAction<TEvent, TProjection, TContext> AsCreateIfDoesNotExistOf(
                Func<TEvent, TKey> getKey)
            {
                return AsCreateOf(getKey).IgnoringDuplicates();
            }

            public ICreateAction<TEvent, TProjection, TContext> AsCreateOrUpdateOf(Func<TEvent, TKey> getKey)
            {
                return AsCreateOf(getKey).OverwritingDuplicates();
            }

            public IDeleteAction<TEvent, TKey, TContext> AsDeleteOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new DeleteAction(actionBuilder, getProjector, getKey);
            }

            public IDeleteAction<TEvent, TKey, TContext> AsDeleteOf(Func<TEvent, TContext, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new DeleteAction(actionBuilder, getProjector, getKey);
            }

            public IDeleteAction<TEvent, TKey, TContext> AsDeleteIfExistsOf(Func<TEvent, TKey> getKey)
            {
                return AsDeleteOf(getKey).IgnoringMisses();
            }

            public IUpdateAction<TEvent, TKey, TProjection, TContext> AsUpdateOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new UpdateAction(actionBuilder, getProjector, (anEvent, context) => getKey(anEvent));
            }

            public IUpdateAction<TEvent, TKey, TProjection, TContext> AsUpdateOf(Func<TEvent, TContext, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new UpdateAction(actionBuilder, getProjector, getKey);
            }

            public IUpdateAction<TEvent, TKey, TProjection, TContext> AsUpdateIfExistsOf(Func<TEvent, TKey> getKey)
            {
                return AsUpdateOf(getKey).IgnoringMisses();
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                actionBuilder.As((anEvent, context) => getProjector().Custom(context, () => action(anEvent, context)));
            }

            IAction<TEvent, TContext> IAction<TEvent, TContext>.When(
                Func<TEvent, TContext, Task<bool>> predicate)
            {
                return When(predicate);
            }

            public ICrudAction<TEvent, TProjection, TKey, TContext> When(
                Func<TEvent, TContext, Task<bool>> predicate)
            {
                if (predicate == null)
                {
                    throw new ArgumentNullException(nameof(predicate));
                }

                actionBuilder.When(predicate);
                return this;
            }

            private sealed class CreateAction : ICreateAction<TEvent, TProjection, TContext>
            {
                private Func<TProjection, TEvent, TContext, bool> shouldOverwrite;

                private readonly IAction<TEvent, TContext> actionBuilder;
                private readonly Func<ProjectorMap<TProjection, TKey, TContext>> projector;
                private readonly Func<TEvent, TContext, TKey> getKey;

                public CreateAction(IAction<TEvent, TContext> actionBuilder,
                    Func<ProjectorMap<TProjection, TKey, TContext>> projector, Func<TEvent, TContext, TKey> getKey)
                {
                    this.actionBuilder = actionBuilder;
                    this.projector = projector;
                    this.getKey = getKey;

                    shouldOverwrite = (existingProjection, @event, context) =>
                        throw new ProjectionException(
                            $"Projection {typeof(TProjection)} with key {getKey(@event,context)}already exists.");
                }

                public CreateAction(IAction<TEvent, TContext> actionBuilder,
                    Func<ProjectorMap<TProjection, TKey, TContext>> projector, Func<TEvent, TKey> getKey)
                {
                    this.actionBuilder = actionBuilder;
                    this.projector = projector;
                    this.getKey = (@event, context) =>getKey(@event);

                    shouldOverwrite = (existingProjection, @event, context) =>
                        throw new ProjectionException(
                            $"Projection {typeof(TProjection)} with key {getKey(@event)}already exists.");
                }

                public ICreateAction<TEvent, TProjection, TContext> Using(Func<TProjection, TEvent, TContext, Task> projector)
                {
                    if (projector == null)
                    {
                        throw new ArgumentNullException(nameof(projector));
                    }

                    actionBuilder.As((anEvent, context) => this.projector().Create(
                            getKey(anEvent,context),
                            context,
                            projection => projector(projection, anEvent, context),
                            existingProjection => shouldOverwrite(existingProjection, anEvent, context)));

                    return this;
                }

                public ICreateAction<TEvent, TProjection, TContext>  IgnoringDuplicates()
                {
                    shouldOverwrite = (duplicate, @event,context) => false;
                    return this;
                }

                public ICreateAction<TEvent, TProjection, TContext> OverwritingDuplicates()
                {
                    shouldOverwrite = (duplicate, @event,context) => true;
                    return this;
                }

                public ICreateAction<TEvent, TProjection, TContext> HandlingDuplicatesUsing(Func<TProjection, TEvent, TContext, bool> shouldOverwrite)
                {
                    this.shouldOverwrite = shouldOverwrite;
                    return this;
                }
            }

            private sealed class UpdateAction : IUpdateAction<TEvent, TKey, TProjection, TContext>
            {
                private readonly IAction<TEvent, TContext> actionBuilder;
                private readonly Func<ProjectorMap<TProjection, TKey, TContext>> projector;
                private readonly Func<TEvent, TContext, TKey> getKey;
                private Func<TKey, TContext, bool> handleMissesUsing;

                public UpdateAction(IAction<TEvent, TContext> actionBuilder,
                    Func<ProjectorMap<TProjection, TKey, TContext>> projector, Func<TEvent, TContext, TKey> getKey)
                {
                    this.projector = projector;
                    this.actionBuilder = actionBuilder;
                    this.getKey = getKey;

                    ThrowingIfMissing();
                }

                public IUpdateAction<TEvent, TKey, TProjection, TContext> Using(Func<TProjection, TEvent, TContext, Task> updateAction)
                {
                    if (updateAction == null)
                    {
                        throw new ArgumentNullException(nameof(updateAction));
                    }

                    actionBuilder.As((anEvent, context) => OnUpdate(updateAction, anEvent, context));

                    return this;
                }

                private async Task OnUpdate(Func<TProjection, TEvent, TContext, Task> projector, TEvent anEvent, TContext context)
                {
                    var key = getKey(anEvent,context);
                    
                    await this.projector().Update(
                        key,
                        context,
                        projection => projector(projection, anEvent, context),
                        () => handleMissesUsing(key, context));
                }

                public IUpdateAction<TEvent, TKey, TProjection, TContext> ThrowingIfMissing()
                {
                    handleMissesUsing = (key, ctx) => throw new ProjectionException($"Failed to find {typeof(TProjection).Name} with key {key}");
                    return this;
                }

                public IUpdateAction<TEvent, TKey, TProjection, TContext> IgnoringMisses()
                {
                    handleMissesUsing = (_, __) => false;
                    return this;
                }

                public IUpdateAction<TEvent, TKey, TProjection, TContext> CreatingIfMissing()
                {
                    handleMissesUsing = (_, __) => true;
                    return this;
                }

                public IUpdateAction<TEvent, TKey, TProjection, TContext> HandlingMissesUsing(Func<TKey, TContext, bool> action)
                {
                    handleMissesUsing = action;
                    return this;
                }
            }

            private class DeleteAction : IDeleteAction<TEvent, TKey, TContext>
            {
                private Action<TKey, TContext> handleMissing;

                public DeleteAction(IAction<TEvent, TContext> actionBuilder,
                    Func<ProjectorMap<TProjection, TKey, TContext>> projector, Func<TEvent, TContext, TKey> getKey)
                {
                    actionBuilder.As((anEvent, context) => OnDelete(projector(), getKey, anEvent, context));

                    ThrowingIfMissing();
                }

                public DeleteAction(IAction<TEvent, TContext> actionBuilder,
                    Func<ProjectorMap<TProjection, TKey, TContext>> projector, Func<TEvent, TKey> getKey)
                {
                    actionBuilder.As((anEvent, context) => OnDelete(projector(), (anEvent1, context1) => getKey(anEvent1), anEvent, context));

                    ThrowingIfMissing();
                }

                private async Task OnDelete(ProjectorMap<TProjection, TKey, TContext> projector, Func<TEvent, TContext, TKey> getKey, TEvent anEvent, TContext context)
                {
                    TKey key = getKey(anEvent, context);
                    bool deleted = await projector.Delete(key, context);
                    if (!deleted)
                    {
                        handleMissing(key, context);
                    }
                }

                public IDeleteAction<TEvent, TKey, TContext> ThrowingIfMissing()
                {
                    handleMissing = (key, ctx) => throw new ProjectionException($"Could not delete {typeof(TProjection).Name} with key {key} because it does not exist");;
                    return this;
                }

                public IDeleteAction<TEvent, TKey, TContext> IgnoringMisses()
                {
                    handleMissing = (_, __) => {};
                    return this;
                }

                public IDeleteAction<TEvent, TKey, TContext> HandlingMissesUsing(Action<TKey, TContext> action)
                {
                    handleMissing = action;
                    return this;
                }
            }
        }
    }
}