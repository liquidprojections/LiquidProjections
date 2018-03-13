using System;
using System.Threading.Tasks;
using LiquidProjections.MapBuilding;

namespace LiquidProjections
{
    /// <summary>
    /// Contains extension methods to map events to handlers in a fluent fashion.
    /// </summary>
    public static class EventMapBuilderExtensions
    {
        /// <summary>
        /// Finishes configuring a custom handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="actionBuilder">The <see cref="IAction{TEvent,TContext}"/>.</param>
        /// <param name="action">
        /// The synchronous delegate that handles the event.
        /// Takes the event and the context as the parameters.
        /// </param>
        public static void As<TEvent, TContext>(
            this IAction<TEvent, TContext> actionBuilder,
            Action<TEvent, TContext> action)
        {
            if (actionBuilder == null)
            {
                throw new ArgumentNullException(nameof(actionBuilder));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            actionBuilder.As((anEvent, context) =>
            {
                action(anEvent, context);
                return SpecializedTasks.ZeroTask;
            });
        }

        /// <summary>
        /// Finishes configuring a custom handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="actionBuilder">The <see cref="IAction{TEvent,TContext}"/>.</param>
        /// <param name="action">
        /// The synchronous delegate that handles the event.
        /// Takes the event as the parameter.
        /// </param>
        public static void As<TEvent, TContext>(
            this IAction<TEvent, TContext> actionBuilder,
            Action<TEvent> action)
        {
            if (actionBuilder == null)
            {
                throw new ArgumentNullException(nameof(actionBuilder));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            actionBuilder.As((anEvent, context) =>
            {
                action(anEvent);
                return SpecializedTasks.ZeroTask;
            });
        }

        /// <summary>
        /// Finishes configuring a custom handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="actionBuilder">The <see cref="IAction{TEvent,TContext}"/>.</param>
        /// <param name="action">
        /// The asynchronous delegate that handles the event.
        /// Takes the event as the parameter.
        /// </param>
        public static void As<TEvent, TContext>(
            this IAction<TEvent, TContext> actionBuilder,
            Func<TEvent, Task> action)
        {
            if (actionBuilder == null)
            {
                throw new ArgumentNullException(nameof(actionBuilder));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            actionBuilder.As((anEvent, context) => action(anEvent));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="actionBuilder">The <see cref="IAction{TEvent,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event and the context as the parameters.
        /// </param>
        /// <returns>
        /// <see cref="IAction{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IAction<TEvent, TContext> When<TEvent, TContext>(
            this IAction<TEvent, TContext> actionBuilder,
            Func<TEvent, TContext, bool> predicate)
        {
            if (actionBuilder == null)
            {
                throw new ArgumentNullException(nameof(actionBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return actionBuilder.When((anEvent, context) => Task.FromResult(predicate(anEvent, context)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="actionBuilder">The <see cref="IAction{TEvent,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="IAction{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IAction<TEvent, TContext> When<TEvent, TContext>(
            this IAction<TEvent, TContext> actionBuilder,
            Func<TEvent, bool> predicate)
        {
            if (actionBuilder == null)
            {
                throw new ArgumentNullException(nameof(actionBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return actionBuilder.When((anEvent, context) => Task.FromResult(predicate(anEvent)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="actionBuilder">The <see cref="IAction{TEvent,TContext}"/>.</param>
        /// <param name="predicate">
        /// The asynchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="IAction{TEvent,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static IAction<TEvent, TContext> When<TEvent, TContext>(
            this IAction<TEvent, TContext> actionBuilder,
            Func<TEvent, Task<bool>> predicate)
        {
            if (actionBuilder == null)
            {
                throw new ArgumentNullException(nameof(actionBuilder));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return actionBuilder.When((anEvent, context) => predicate(anEvent));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="crudAction">The <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event and the context as the parameters.
        /// </param>
        /// <returns>
        /// <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static ICrudAction<TEvent, TProjection, TKey, TContext> When<TEvent, TProjection, TKey, TContext>(
            this ICrudAction<TEvent, TProjection, TKey, TContext> crudAction,
            Func<TEvent, TContext, bool> predicate)
        {
            if (crudAction == null)
            {
                throw new ArgumentNullException(nameof(crudAction));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return crudAction.When((anEvent, context) => Task.FromResult(predicate(anEvent, context)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="crudAction">The <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/>.</param>
        /// <param name="predicate">
        /// The synchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static ICrudAction<TEvent, TProjection, TKey, TContext> When<TEvent, TProjection, TKey, TContext>(
            this ICrudAction<TEvent, TProjection, TKey, TContext> crudAction,
            Func<TEvent, bool> predicate)
        {
            if (crudAction == null)
            {
                throw new ArgumentNullException(nameof(crudAction));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return crudAction.When((anEvent, context) => Task.FromResult(predicate(anEvent)));
        }

        /// <summary>
        /// Continues configuring a handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> with key of type <typeparamref name="TKey"/>
        /// using context of type <typeparamref name="TContext"/>.
        /// Provides an additional condition that needs to be satisfied in order for the event to be handled by the handler.
        /// </summary>
        /// <param name="crudAction">The <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/>.</param>
        /// <param name="predicate">
        /// The asynchronous delegate that filters the events and
        /// should return <c>true</c> for events that will be handled by the handler.
        /// Takes the event as the parameter.
        /// </param>
        /// <returns>
        /// <see cref="ICrudAction{TEvent,TProjection,TKey,TContext}"/> that allows to continue configuring the handler.
        /// </returns>
        public static ICrudAction<TEvent, TProjection, TKey, TContext> When<TEvent, TProjection, TKey, TContext>(
            this ICrudAction<TEvent, TProjection, TKey, TContext> crudAction,
            Func<TEvent, Task<bool>> predicate)
        {
            if (crudAction == null)
            {
                throw new ArgumentNullException(nameof(crudAction));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return crudAction.When((anEvent, context) => predicate(anEvent));
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="action">The <see cref="ICreateAction{TEvent,TProjection,TContext}"/>.</param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateAction<TEvent, TProjection, TContext> action,
            Action<TProjection, TEvent, TContext> projector)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            action.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent, context);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="action">The <see cref="ICreateAction{TEvent,TProjection,TContext}"/>.</param>
        /// <param name="projector">
        /// The synchronous delegate that initializes the created projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateAction<TEvent, TProjection, TContext> action,
            Action<TProjection, TEvent> projector)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            action.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection creation handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="action">The <see cref="ICreateAction{TEvent,TProjection,TContext}"/>.</param>
        /// <param name="projector">
        /// The asynchronous delegate that initializes the created projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TProjection, TContext>(
            this ICreateAction<TEvent, TProjection, TContext> action,
            Func<TProjection, TEvent, Task> projector)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            action.Using((projection, anEvent, context) => projector(projection, anEvent));
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
        /// <param name="action">
        /// The <see cref="IUpdateAction{TEvent,TKey,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that updates the projection.
        /// Takes the projection, the event and the context as the parameters.
        /// </param>
        public static void Using<TEvent, TKey, TProjection, TContext>(
            this IUpdateAction<TEvent, TKey, TProjection, TContext> action,
            Action<TProjection, TEvent, TContext> projector)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            action.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent, context);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="action">
        /// The <see cref="IUpdateAction{TEvent,TKey,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The synchronous delegate that updates the projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TKey, TProjection, TContext>(
            this IUpdateAction<TEvent, TKey, TProjection, TContext> action,
            Action<TProjection, TEvent> projector)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            action.Using((projection, anEvent, context) =>
            {
                projector(projection, anEvent);
                return SpecializedTasks.FalseTask;
            });
        }

        /// <summary>
        /// Finishes configuring a projection updating handler for events of type <typeparamref name="TEvent"/>
        /// for projections of type <typeparamref name="TProjection"/> using context of type <typeparamref name="TContext"/>.
        /// </summary>
        /// <param name="action">
        /// The <see cref="IUpdateAction{TEvent,TKey,TProjection,TContext}"/>.
        /// </param>
        /// <param name="projector">
        /// The asynchronous delegate that updates the projection.
        /// Takes the projection and the event as the parameters.
        /// </param>
        public static void Using<TEvent, TKey, TProjection, TContext>(
            this IUpdateAction<TEvent, TKey, TProjection, TContext> action,
            Func<TProjection, TEvent, Task> projector)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (projector == null)
            {
                throw new ArgumentNullException(nameof(projector));
            }

            action.Using((projection, anEvent, context) => projector(projection, anEvent));
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