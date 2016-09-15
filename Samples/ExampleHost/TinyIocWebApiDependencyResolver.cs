using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Dependencies;
using TinyIoC;

namespace LiquidProjections.ExampleHost
{
    internal class TinyIocWebApiDependencyResolver : IDependencyResolver
    {
        private bool disposed;
        private readonly TinyIoCContainer container;

        public TinyIocWebApiDependencyResolver(TinyIoCContainer container)
        {
            if (container == null)
            {
                throw new ArgumentNullException("container");
            }

            this.container = container;
        }

        public IDependencyScope BeginScope()
        {
            if (disposed)
            {
                throw new ObjectDisposedException("this", "This scope has already been disposed.");
            }

            return new TinyIocWebApiDependencyResolver(container.GetChildContainer());
        }

        public object GetService(Type serviceType)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("this", "This scope has already been disposed.");
            }

            try
            {
                return container.Resolve(serviceType);
            }
            catch (TinyIoCResolutionException)
            {
                return null;
            }
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            if (disposed)
            {
                throw new ObjectDisposedException("this", "This scope has already been disposed.");
            }

            try
            {
                return container.ResolveAll(serviceType);
            }
            catch (TinyIoCResolutionException)
            {
                return Enumerable.Empty<object>();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                container.Dispose();
            }

            disposed = true;
        }
    }
}