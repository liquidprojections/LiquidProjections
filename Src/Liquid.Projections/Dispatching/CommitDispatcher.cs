using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using eVision.QueryHost.Logging;

namespace eVision.QueryHost.Dispatching
{
    public class CommitDispatcher<TUnitOfWork> : IDispatchCommits where TUnitOfWork : IDisposable
    {
        // ReSharper disable StaticFieldInGenericType
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        // ReSharper restore StaticFieldInGenericType

        private readonly Func<TUnitOfWork> unitOfWorkFactory;
        private readonly Func<TUnitOfWork, Task> commitUnitOfWork;
        private readonly ProjectorRegistry<TUnitOfWork> projectorRegistry;

        public CommitDispatcher(Func<TUnitOfWork> unitOfWorkFactory, Func<TUnitOfWork, Task> commitUnitOfWork,
            ProjectorRegistry<TUnitOfWork> projectorRegistry)
        {
            this.unitOfWorkFactory = unitOfWorkFactory;
            this.commitUnitOfWork = commitUnitOfWork;
            this.projectorRegistry = projectorRegistry;
        }

        public virtual async Task Dispatch(Transaction transaction, CancellationToken cancellationToken)
        {
            try
            {
                using (TUnitOfWork context = unitOfWorkFactory())
                {
                    var projectorInstancesCache = new ConcurrentDictionary<Type, object>();

                    foreach (Envelope @event in transaction.Events)
                    {
                        await ProcessEvent(transaction, @event, projectorInstancesCache, context, cancellationToken);
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await commitUnitOfWork(context);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Fatal, () => "Unable to dispatch.", ex);
                throw;
            }
        }

        private async Task ProcessEvent(Transaction transaction, Envelope envelope,
            ConcurrentDictionary<Type, object> projectorInstancesCache,
            TUnitOfWork uow, CancellationToken cancellationToken)
        {
            Type eventType = envelope.Body.GetType();

            foreach (Type projectorType in projectorRegistry.GetProjectorsHandling(eventType))
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    object projector = ResolveProjector(projectorType, projectorInstancesCache, uow);
                    await (Task) projectorType
                        .GetMethod("Handle", new[] {eventType, typeof (ProjectionContext)})
                        .Invoke(projector, new[]
                        {
                            envelope.Body,
                            new ProjectionContext
                            {
                                StreamId = transaction.StreamId,
                                CheckPoint = transaction.Checkpoint,
                                Headers = envelope.Headers,
                                CommitStamp = transaction.TimeStamp
                            }
                        });
                }
            }
        }

        private object ResolveProjector(Type type, ConcurrentDictionary<Type, object> projectorInstancesCache,
            TUnitOfWork uow)
        {
            return projectorInstancesCache.GetOrAdd(type, t => projectorRegistry.Get(t, uow));
        }
    }
}