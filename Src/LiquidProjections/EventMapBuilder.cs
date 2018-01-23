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
        private bool isBuilt;
        private CustomHandler<TContext> customHandler;

        /// <summary>
        /// Starts configuring a new handler for events of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public IEventMappingBuilder<TEvent, TContext> Map<TEvent>()
        {
            AssertNotBuilt();

            return new EventMappingBuilder<TEvent>(this);
        }

        /// <summary>
        /// Builds the resulting event map. Can only be called once.
        /// No changes can be made after the event map has been built.
        /// </summary>
        public IEventMap<TContext> Build()
        {
            AssertNotBuilt();
            AssertComplete();

            isBuilt = true;
            return eventMap;
        }

        /// <summary>
        /// Configures the event map to handle custom actions via the provided delegate <paramref name="handler"/>.
        /// </summary>
        public void HandleCustomActionsAs(CustomHandler<TContext> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (customHandler != null)
            {
                throw new InvalidOperationException(
                    $"{nameof(IEventMapBuilder<TContext>.HandleCustomActionsAs)} was already called.");
            }

            AssertNotBuilt();

            customHandler = handler;
        }

        internal void Add<TEvent>(Func<TEvent, TContext, Task> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AssertNotBuilt();

            eventMap.Add(action);
        }

        internal void AssertNotBuilt()
        {
            if (isBuilt)
            {
                throw new InvalidOperationException("The event map has already been built.");
            }
        }

        private void AssertComplete()
        {
            if (customHandler == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(IEventMapBuilder<TContext>.HandleCustomActionsAs)} was not called.");
            }
        }

        internal sealed class EventMappingBuilder<TEvent> : IEventMappingBuilder<TEvent, TContext>
        {
            private readonly EventMapBuilder<TContext> eventMapBuilder;

            private readonly List<Func<TEvent, TContext, Task<bool>>> predicates =
                new List<Func<TEvent, TContext, Task<bool>>>();

            public EventMappingBuilder(EventMapBuilder<TContext> eventMapBuilder)
            {
                this.eventMapBuilder = eventMapBuilder;
            }

            public IEventMappingBuilder<TEvent, TContext> When(Func<TEvent, TContext, Task<bool>> predicate)
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

                Add((anEvent, context) => eventMapBuilder.customHandler(context, async () => await action(anEvent, context)));
            }

            internal void Add(Func<TEvent, TContext, Task> action)
            {
                eventMapBuilder.Add<TEvent>(async (anEvent, context) =>
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
        private ProjectionModificationHandler<TProjection, TKey, TContext> projectionModificationHandler;
        private ProjectionDeletionHandler<TKey, TContext> projectionDeletionHandler;

        /// <summary>
        /// Starts configuring a new handler for events of type <typeparamref name="TEvent"/>.
        /// </summary>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public IEventMappingBuilder<TEvent, TProjection, TKey, TContext> Map<TEvent>()
        {
            innerBuilder.AssertNotBuilt();

            return new ProjectionEventMappingBuilder<TEvent>(this);
        }

        /// <summary>
        /// Builds the resulting event map. Can only be called once.
        /// No changes can be made after the event map has been built.
        /// </summary>
        public IEventMap<TContext> Build()
        {
            innerBuilder.AssertNotBuilt();
            AssertComplete();

            return innerBuilder.Build();
        }

        /// <summary>
        /// Configures the event map to handle custom actions via the provided delegate <paramref name="handler"/>.
        /// </summary>
        public void HandleCustomActionsAs(CustomHandler<TContext> handler)
        {
            innerBuilder.HandleCustomActionsAs(handler);
        }

        /// <summary>
        /// Configures the event map to handle projection creation and updating
        /// via the provided delegate <paramref name="handler"/>.
        /// </summary>
        public void HandleProjectionModificationsAs(ProjectionModificationHandler<TProjection, TKey, TContext> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (projectionModificationHandler != null)
            {
                throw new InvalidOperationException(
                    $"{nameof(IEventMapBuilder<TProjection, TKey, TContext>.HandleProjectionModificationsAs)} " +
                    "was already called.");
            }

            innerBuilder.AssertNotBuilt();

            projectionModificationHandler = handler;
        }

        /// <summary>
        /// Configures the event map to handle projection deletion
        /// via the provided delegate <paramref name="handler"/>.
        /// </summary>
        public void HandleProjectionDeletionsAs(ProjectionDeletionHandler<TKey, TContext> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (projectionDeletionHandler != null)
            {
                throw new InvalidOperationException(
                    $"{nameof(IEventMapBuilder<TProjection, TKey, TContext>.HandleProjectionDeletionsAs)} was already called.");
            }

            innerBuilder.AssertNotBuilt();

            projectionDeletionHandler = handler;
        }

        private void AssertComplete()
        {
            if (projectionModificationHandler == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(IEventMapBuilder<TProjection, TKey, TContext>.HandleProjectionModificationsAs)} was not called.");
            }

            if (projectionDeletionHandler == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(IEventMapBuilder<TProjection, TKey, TContext>.HandleProjectionDeletionsAs)} was not called.");
            }
        }

        private sealed class ProjectionEventMappingBuilder<TEvent> : IEventMappingBuilder<TEvent, TProjection, TKey, TContext>
        {
            private static readonly ProjectionDeletionOptions optionsForDelete =
                new ProjectionDeletionOptions(MissingProjectionDeletionBehavior.Throw);

            private static readonly ProjectionDeletionOptions optionsForDeleteIfExists =
                new ProjectionDeletionOptions(MissingProjectionDeletionBehavior.Ignore);

            private readonly EventMapBuilder<TContext>.EventMappingBuilder<TEvent> innerBuilder;
            private readonly EventMapBuilder<TProjection, TKey, TContext> eventMapBuilder;

            public ProjectionEventMappingBuilder(EventMapBuilder<TProjection, TKey, TContext> eventMapBuilder)
            {
                innerBuilder = new EventMapBuilder<TContext>.EventMappingBuilder<TEvent>(eventMapBuilder.innerBuilder);
                this.eventMapBuilder = eventMapBuilder;
            }

            public ICreateEventActionBuilder<TEvent, TProjection, TContext> AsCreateOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new CreateEventActionBuilder(this, getKey);
            }

            public ICreateIfDoesNotExistEventActionBuilder<TEvent, TProjection, TContext> AsCreateIfDoesNotExistOf(
                Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new CreateIfDoesNotExistEventActionBuilder(this, getKey);
            }

            public ICreateOrUpdateEventActionBuilder<TEvent, TProjection, TContext> AsCreateOrUpdateOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new CreateOrUpdateEventActionBuilder(this, getKey);
            }

            public void AsDeleteOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                innerBuilder.Add((anEvent, context) =>
                    eventMapBuilder.projectionDeletionHandler(getKey(anEvent), context, optionsForDelete));
            }

            public void AsDeleteIfExistsOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                innerBuilder.Add((anEvent, context) =>
                    eventMapBuilder.projectionDeletionHandler(getKey(anEvent), context, optionsForDeleteIfExists));
            }

            public IUpdateEventActionBuilder<TEvent, TProjection, TContext> AsUpdateOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new UpdateEventActionBuilder(this, getKey);
            }

            public IUpdateIfExistsEventActionBuilder<TEvent, TProjection, TContext> AsUpdateIfExistsOf(Func<TEvent, TKey> getKey)
            {
                if (getKey == null)
                {
                    throw new ArgumentNullException(nameof(getKey));
                }

                return new UpdateIfExistsEventActionBuilder(this, getKey);
            }

            public void As(Func<TEvent, TContext, Task> action)
            {
                innerBuilder.As(action);
            }

            IEventMappingBuilder<TEvent, TContext> IEventMappingBuilder<TEvent, TContext>.When(
                Func<TEvent, TContext, Task<bool>> predicate)
            {
                return When(predicate);
            }

            public IEventMappingBuilder<TEvent, TProjection, TKey, TContext> When(
                Func<TEvent, TContext, Task<bool>> predicate)
            {
                if (predicate == null)
                {
                    throw new ArgumentNullException(nameof(predicate));
                }

                innerBuilder.When(predicate);
                return this;
            }

            private sealed class CreateEventActionBuilder :
                ICreateEventActionBuilder<TEvent, TProjection, TContext>
            {
                private static readonly ProjectionModificationOptions options = new ProjectionModificationOptions(
                    MissingProjectionModificationBehavior.Create,
                    ExistingProjectionModificationBehavior.Throw);

                private readonly ProjectionEventMappingBuilder<TEvent> eventMappingBuilder;
                private readonly Func<TEvent, TKey> getKey;

                public CreateEventActionBuilder(
                    ProjectionEventMappingBuilder<TEvent> eventMappingBuilder,
                    Func<TEvent, TKey> getKey)
                {
                    this.eventMappingBuilder = eventMappingBuilder;
                    this.getKey = getKey;
                }

                public void Using(Func<TProjection, TEvent, TContext, Task> projector)
                {
                    if (projector == null)
                    {
                        throw new ArgumentNullException(nameof(projector));
                    }

                    eventMappingBuilder.innerBuilder.Add((anEvent, context) =>
                        eventMappingBuilder.eventMapBuilder.projectionModificationHandler(
                            getKey(anEvent),
                            context,
                            projection => projector(projection, anEvent, context),
                            options));
                }
            }

            private sealed class CreateIfDoesNotExistEventActionBuilder :
                ICreateIfDoesNotExistEventActionBuilder<TEvent, TProjection, TContext>
            {
                private static readonly ProjectionModificationOptions options = new ProjectionModificationOptions(
                    MissingProjectionModificationBehavior.Create,
                    ExistingProjectionModificationBehavior.Ignore);

                private readonly ProjectionEventMappingBuilder<TEvent> eventMappingBuilder;
                private readonly Func<TEvent, TKey> getKey;

                public CreateIfDoesNotExistEventActionBuilder(
                    ProjectionEventMappingBuilder<TEvent> eventMappingBuilder,
                    Func<TEvent, TKey> getKey)
                {
                    this.eventMappingBuilder = eventMappingBuilder;
                    this.getKey = getKey;
                }

                public void Using(Func<TProjection, TEvent, TContext, Task> projector)
                {
                    if (projector == null)
                    {
                        throw new ArgumentNullException(nameof(projector));
                    }

                    eventMappingBuilder.innerBuilder.Add((anEvent, context) =>
                        eventMappingBuilder.eventMapBuilder.projectionModificationHandler(
                            getKey(anEvent),
                            context,
                            projection => projector(projection, anEvent, context),
                            options));
                }
            }

            private sealed class UpdateEventActionBuilder :
                IUpdateEventActionBuilder<TEvent, TProjection, TContext>
            {
                private static readonly ProjectionModificationOptions options = new ProjectionModificationOptions(
                    MissingProjectionModificationBehavior.Throw,
                    ExistingProjectionModificationBehavior.Update);

                private readonly ProjectionEventMappingBuilder<TEvent> eventMappingBuilder;
                private readonly Func<TEvent, TKey> getKey;

                public UpdateEventActionBuilder(
                    ProjectionEventMappingBuilder<TEvent> eventMappingBuilder,
                    Func<TEvent, TKey> getKey)
                {
                    this.eventMappingBuilder = eventMappingBuilder;
                    this.getKey = getKey;
                }

                public void Using(Func<TProjection, TEvent, TContext, Task> projector)
                {
                    if (projector == null)
                    {
                        throw new ArgumentNullException(nameof(projector));
                    }

                    eventMappingBuilder.innerBuilder.Add((anEvent, context) =>
                        eventMappingBuilder.eventMapBuilder.projectionModificationHandler(
                            getKey(anEvent),
                            context,
                            projection => projector(projection, anEvent, context),
                            options));
                }
            }

            private sealed class UpdateIfExistsEventActionBuilder :
                IUpdateIfExistsEventActionBuilder<TEvent, TProjection, TContext>
            {
                private static readonly ProjectionModificationOptions options = new ProjectionModificationOptions(
                    MissingProjectionModificationBehavior.Ignore,
                    ExistingProjectionModificationBehavior.Update);

                private readonly ProjectionEventMappingBuilder<TEvent> eventMappingBuilder;
                private readonly Func<TEvent, TKey> getKey;

                public UpdateIfExistsEventActionBuilder(
                    ProjectionEventMappingBuilder<TEvent> eventMappingBuilder,
                    Func<TEvent, TKey> getKey)
                {
                    this.eventMappingBuilder = eventMappingBuilder;
                    this.getKey = getKey;
                }

                public void Using(Func<TProjection, TEvent, TContext, Task> projector)
                {
                    if (projector == null)
                    {
                        throw new ArgumentNullException(nameof(projector));
                    }

                    eventMappingBuilder.innerBuilder.Add((anEvent, context) =>
                        eventMappingBuilder.eventMapBuilder.projectionModificationHandler(
                            getKey(anEvent),
                            context,
                            projection => projector(projection, anEvent, context),
                            options));
                }
            }

            private sealed class CreateOrUpdateEventActionBuilder :
                ICreateOrUpdateEventActionBuilder<TEvent, TProjection, TContext>
            {
                private static readonly ProjectionModificationOptions options = new ProjectionModificationOptions(
                    MissingProjectionModificationBehavior.Create,
                    ExistingProjectionModificationBehavior.Update);

                private readonly ProjectionEventMappingBuilder<TEvent> eventMappingBuilder;
                private readonly Func<TEvent, TKey> getKey;

                public CreateOrUpdateEventActionBuilder(
                    ProjectionEventMappingBuilder<TEvent> eventMappingBuilder,
                    Func<TEvent, TKey> getKey)
                {
                    this.eventMappingBuilder = eventMappingBuilder;
                    this.getKey = getKey;
                }

                public void Using(Func<TProjection, TEvent, TContext, Task> projector)
                {
                    if (projector == null)
                    {
                        throw new ArgumentNullException(nameof(projector));
                    }

                    eventMappingBuilder.innerBuilder.Add((anEvent, context) =>
                        eventMappingBuilder.eventMapBuilder.projectionModificationHandler(
                            getKey(anEvent),
                            context,
                            projection => projector(projection, anEvent, context),
                            options));
                }
            }
        }
    }

    /// <summary>
    /// Contains extension methods to map events to handlers in a fluent fashion.
    /// </summary>
    public static class EventMapBuilderExtensions
    {
        /// <summary>
        /// Finishes configuring a custom handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TContext}"/>.</param>
        /// <param name="action">
        /// The synchronous delegate that handles the event.
        /// Takes the event and the context as the parameters.
        /// </param>
        public static void As<TEvent, TContext>(
            this IEventMappingBuilder<TEvent, TContext> eventMappingBuilder,
            Action<TEvent, TContext> action)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventMappingBuilder.As((anEvent, context) =>
            {
                action(anEvent, context);
                return SpecializedTasks.ZeroTask;
            });
        }

        /// <summary>
        /// Finishes configuring a custom handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TContext}"/>.</param>
        /// <param name="action">
        /// The synchronous delegate that handles the event.
        /// Takes the event as the parameter.
        /// </param>
        public static void As<TEvent, TContext>(
            this IEventMappingBuilder<TEvent, TContext> eventMappingBuilder,
            Action<TEvent> action)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventMappingBuilder.As((anEvent, context) =>
            {
                action(anEvent);
                return SpecializedTasks.ZeroTask;
            });
        }

        /// <summary>
        /// Finishes configuring a custom handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TContext}"/>.</param>
        /// <param name="action">
        /// The asynchronous delegate that handles the event.
        /// Takes the event as the parameter.
        /// </param>
        public static void As<TEvent, TContext>(
            this IEventMappingBuilder<TEvent, TContext> eventMappingBuilder,
            Func<TEvent, Task> action)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            eventMappingBuilder.As((anEvent, context) => action(anEvent));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event and the context as the parameters.
        /// </param>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IEventMappingBuilder<TEvent, TContext> When<TEvent, TContext>(
            this IEventMappingBuilder<TEvent, TContext> eventMappingBuilder,
            Func<TEvent, TContext, bool> predicate)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return eventMappingBuilder.When((anEvent, context) => Task.FromResult(predicate(anEvent, context)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IEventMappingBuilder<TEvent, TContext> When<TEvent, TContext>(
            this IEventMappingBuilder<TEvent, TContext> eventMappingBuilder,
            Func<TEvent, bool> predicate)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return eventMappingBuilder.When((anEvent, context) => Task.FromResult(predicate(anEvent)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TContext}"/>.</param>
        /// <param name="predicate">
        /// The asynchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IEventMappingBuilder<TEvent, TContext> When<TEvent, TContext>(
            this IEventMappingBuilder<TEvent, TContext> eventMappingBuilder,
            Func<TEvent, Task<bool>> predicate)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return eventMappingBuilder.When((anEvent, context) => predicate(anEvent));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TProjection,TKey,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event and the context as the parameters.
        /// </param>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IEventMappingBuilder<TEvent, TProjection, TKey, TContext> When<TEvent, TProjection, TKey, TContext>(
            this IEventMappingBuilder<TEvent, TProjection, TKey, TContext> eventMappingBuilder,
            Func<TEvent, TContext, bool> predicate)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return eventMappingBuilder.When((anEvent, context) => Task.FromResult(predicate(anEvent, context)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TProjection,TKey,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IEventMappingBuilder<TEvent, TProjection, TKey, TContext> When<TEvent, TProjection, TKey, TContext>(
            this IEventMappingBuilder<TEvent, TProjection, TKey, TContext> eventMappingBuilder,
            Func<TEvent, bool> predicate)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return eventMappingBuilder.When((anEvent, context) => Task.FromResult(predicate(anEvent)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="eventMappingBuilder">The <see cref="IEventMappingBuilder{TEvent,TProjection,TKey,TContext}"/>.</param>
        /// <param name="predicate">
        /// The asynchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="IEventMappingBuilder{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IEventMappingBuilder<TEvent, TProjection, TKey, TContext> When<TEvent, TProjection, TKey, TContext>(
            this IEventMappingBuilder<TEvent, TProjection, TKey, TContext> eventMappingBuilder,
            Func<TEvent, Task<bool>> predicate)
        {
            if (eventMappingBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventMappingBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return eventMappingBuilder.When((anEvent, context) => predicate(anEvent));
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">The <see cref="ICreateEventActionBuilder{TEvent,TProjection,TContext}"/>.</param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent, TContext> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent, context);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">The <see cref="ICreateEventActionBuilder{TEvent,TProjection,TContext}"/>.</param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">The <see cref="ICreateEventActionBuilder{TEvent,TProjection,TContext}"/>.</param>
        /// <param name="projector">
        /// The asynchronous delegate that initializes the created projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Func<TProjection, TEvent, Task> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) => projector(projection, anEvent));
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for projections which do not exist yet 
        /// for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="ICreateIfDoesNotExistEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateIfDoesNotExistEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent, TContext> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent, context);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for projections which do not exist yet 
        /// for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="ICreateIfDoesNotExistEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateIfDoesNotExistEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for projections which do not exist yet 
        /// for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="ICreateIfDoesNotExistEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The asynchronous delegate that initializes the created projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateIfDoesNotExistEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Func<TProjection, TEvent, Task> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) => projector(projection, anEvent));
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="IUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that updates the projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this IUpdateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent, TContext> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent, context);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="IUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that updates the projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this IUpdateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="IUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The asynchronous delegate that updates the projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this IUpdateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Func<TProjection, TEvent, Task> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) => projector(projection, anEvent));
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for projections which do already exist
        /// for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="IUpdateIfExistsEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that updates the projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this IUpdateIfExistsEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent, TContext> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent, context);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for projections which do already exist
        /// for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="IUpdateIfExistsEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that updates the projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this IUpdateIfExistsEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for projections which do already exist
        /// for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="IUpdateIfExistsEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The asynchronous delegate that updates the projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this IUpdateIfExistsEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Func<TProjection, TEvent, Task> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) => projector(projection, anEvent));
        }

        /// <summary>
        /// Finishes configuring a projection creation or updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="ICreateOrUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection or updates the existing projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateOrUpdateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent, TContext> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent, context);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation or updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="ICreateOrUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection or updates the existing projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateOrUpdateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Action<TProjection, TEvent> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation or updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="eventActionBuilder">
        /// The <see cref="ICreateOrUpdateEventActionBuilder{TEvent,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The asynchronous delegate that initializes the created projection or updates the existing projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateOrUpdateEventActionBuilder<TEvent, TProjection, TContext> eventActionBuilder,
            Func<TProjection, TEvent, Task> projector)
        {
            if (eventActionBuilder == null)
            {
                throw new ArgumentNullException(nameof(eventActionBuilder));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            eventActionBuilder.Using((projection, anEvent, context) => projector(projection, anEvent));
        }
    }
}