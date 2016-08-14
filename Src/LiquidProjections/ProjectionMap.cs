using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class ProjectionMap<TContext> where TContext : ProjectionContext
    {
        private readonly IDictionary<Type, IHandler> mappings = new Dictionary<Type, IHandler>();

        public Action<TEvent> Map<TEvent>()
        {
            return new Action<TEvent>(this);
        }

        public Func<TContext, Task> GetHandler(object @event)
        {
            return mappings[@event.GetType()].GetHandler(@event);
        }

        private void Add(Type type, IHandler handler)
        {
            mappings.Add(type, handler);
        }

        public class Action<TEvent>
        {
            private readonly ProjectionMap<TContext> parent;

            public Action(ProjectionMap<TContext> parent)
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