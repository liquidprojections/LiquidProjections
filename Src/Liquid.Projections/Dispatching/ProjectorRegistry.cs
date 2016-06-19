using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace eVision.QueryHost.Dispatching
{
    public class ProjectorRegistry<TUnitOfWork> : IEnumerable<Type>
    {
        private readonly IDictionary<Type, Func<Type, TUnitOfWork, object>> factoryRegister = 
            new Dictionary<Type, Func<Type, TUnitOfWork, object>>();

        public ProjectorRegistry<TUnitOfWork> Add<TProjector>(Func<TUnitOfWork, TProjector> projectorFactory)
            where TProjector : class
        {
            factoryRegister.Add(typeof (TProjector), (type, uow) => projectorFactory(uow));
            return this;
        }

        public ProjectorRegistry<TUnitOfWork> Add(Type projectorType, Func<Type, TUnitOfWork, object> projectorFactory)
        {
            factoryRegister.Add(projectorType, projectorFactory);
            return this;
        }

        public object Get(Type projectorType, TUnitOfWork uow)
        {
            return factoryRegister[projectorType](projectorType, uow);
        }

        public IEnumerable<Type> GetProjectorsHandling(Type eventType)
        {
            Type denormalizerWithHeadersType = typeof(IProject<>)
                .MakeGenericType(eventType);

            return GetProjectorTypes().Where(denormalizerWithHeadersType.IsAssignableFrom);
        }

        private IEnumerable<Type> GetProjectorTypes()
        {
            return factoryRegister.Keys;
        }

        public IEnumerator<Type> GetEnumerator()
        {
            return factoryRegister.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}