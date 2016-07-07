using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LiquidProjections
{
    public class EventMapCollection<TProjection, TContext> 
    {
        private readonly Dictionary<Type, IEventMap> map = new Dictionary<Type, IEventMap>();

        public void Map<TEvent>(Func<TEvent, string> getKey, Func<TEvent, long> getVersion, Action<TProjection, TEvent> projector)
        {
            Map(getKey, getVersion, (p, e, ctx) =>
            {
                projector(p, e);

                return Task.FromResult(0);
            });
        }

        public void Map<TEvent>(Func<TEvent, string> getKey, Func<TEvent, long> getVersion, Func<TProjection, TEvent, Task> projector)
        {
            Map(getKey, getVersion, (p, e, uow) => projector(p, e));
        }

        public void Map<TEvent>(Func<TEvent, string> getKey, Func<TEvent, long> getVersion, Func<TProjection, TEvent, TContext, Task> projector)
        {
            map[typeof(TEvent)] = new EventMap<TEvent>(getKey, getVersion, projector);
        }

        public string GetKey(object @event)
        {
            return map[@event.GetType()].GetKey(@event);
        }

        public long GetVersion(object @event)
        {
            return map[@event.GetType()].GetVersion(@event);
        }

        public Func<TProjection, TContext, Task> GetHandler(object @event)
        {
            return map.ContainsKey(@event.GetType()) ? map[@event.GetType()].GetHandler(@event) : null;
        }

        private interface IEventMap
        {
            string GetKey(object @event);
            long GetVersion(object @event);
            Func<TProjection, TContext, Task> GetHandler(object @event);
        }

        private class EventMap<TEvent> : IEventMap
        {
            private readonly Func<TEvent, string> getKey;
            private readonly Func<TEvent, long> getVersion;
            private readonly Func<TProjection, TEvent, TContext, Task> projector;

            public EventMap(Func<TEvent, string> getKey, Func<TEvent, long> getVersion, Func<TProjection, TEvent, TContext, Task> projector)
            {
                this.getKey = getKey;
                this.getVersion = getVersion;
                this.projector = projector;
            }

            public string GetKey(object @event)
            {
                return getKey((TEvent)@event);
            }
            public long GetVersion(object @event)
            {
                return getVersion((TEvent)@event);
            }

            public Func<TProjection, TContext, Task> GetHandler(object @event)
            {
                return (projection, uow) => projector(projection, (TEvent)@event, uow);
            }
        }

    }


}