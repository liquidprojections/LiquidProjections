using System;
using System.Threading;
using System.Threading.Tasks;

namespace eVision.QueryHost.Dispatching
{
    public class RetryingCommitDispatcher<TContext> : CommitDispatcher<TContext>
        where TContext : IDisposable
    {
        private readonly ExceptionRetryPolicy retryPolicy;

        public RetryingCommitDispatcher(Func<TContext> unitOfWorkFactory, Func<TContext, Task> commitUnitOfWork, ProjectorRegistry<TContext> projectorRegistry, ExceptionRetryPolicy retryPolicy)
            : base(unitOfWorkFactory, commitUnitOfWork, projectorRegistry)
        {
            this.retryPolicy = retryPolicy;
        }

        public override Task Dispatch(Transaction eventTransaction, CancellationToken cancellationToken)
        {
            return retryPolicy.Retry(() => base.Dispatch(eventTransaction, cancellationToken), cancellationToken);
        }
    }
}