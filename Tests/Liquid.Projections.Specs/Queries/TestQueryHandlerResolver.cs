using System;
using System.Collections.Generic;

namespace eVision.QueryHost.Specs.Queries
{
    public class TestQueryHandlerResolver : IQueryHandlerResolver
    {
        private readonly IDictionary<Type, Func<object>> handlers = new Dictionary<Type, Func<object>>
        {
            { typeof(ExampleQuery), () => new ExampleQueryHandler() },
            { typeof(ExampleWithSerializableQuery), () => new ExampleWithSerializableQueryHandler() },
            { typeof(UnserializableResultQuery), () => new UnserializableResultQueryHandler() },
            { typeof(ThrowingQuery), () => new ThrowingQueryhandler() },
            { typeof(HttpThrowingQuery), () => new HttpThrowingQueryhandler() },
            { typeof(ExampleWithSerializableInterfaceQuery), () => new ExampleWithSerializableQueryHandler() }
        };

        public TestQueryHandlerResolver()
        {
        }

        public TestQueryHandlerResolver(IDictionary<Type, Func<object>> additionalHandlers)
        {
            foreach (var handler in additionalHandlers)
            {
                handlers[handler.Key] = handler.Value;
            }
        }

        public IQueryHandler<TQuery, TResult> Resolve<TQuery, TResult>() where TQuery : IQuery<TResult>
        {
            Func<object> handlerFactory;
            if (handlers.TryGetValue(typeof (TQuery), out handlerFactory))
            {
                return (IQueryHandler<TQuery, TResult>) handlerFactory();
            }
            else
            {
                return null;
            }
        }
    }
}
