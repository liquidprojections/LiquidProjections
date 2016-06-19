using System;
using System.Reactive.Linq;
using System.Reactive.PlatformServices;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using eVision.QueryHost.Common;
using eVision.QueryHost.Logging;

namespace eVision.QueryHost.Dispatching
{
    /// <summary>
    /// Subscribes to a stream of Commits from NEventStore and dispatches to a handler. It tracks the commit stream checkpoint
    /// such that on restart it will continue where it left off.
    /// </summary>
    public sealed class DurableCommitDispatcher : IDisposable
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        private readonly string name;
        private readonly IEventSource eventSource;
        private readonly ICheckpointStore checkpointStore;
        private readonly IDispatchCommits commitDispatcher;
        private readonly Subject<Transaction> dispatchedCommits = new Subject<Transaction>();
        private readonly InterlockedBoolean isStarted = new InterlockedBoolean();
        private int isDisposed;
        private readonly CancellationTokenSource disposed = new CancellationTokenSource();
        private IDisposable subscription;
        private string lastProcessedCheckpoint;
        private IDisposable checkpointStoreSubscription;
        private readonly TimeSpan checkpointStorageInterval;

        static DurableCommitDispatcher()
        {
            // Rx-workaround (it has hardcoded assembly reference which prevents ILMerging the assembly)
            PlatformEnlightenmentProvider.Current = new CurrentPlatformEnlightenmentProvider();
        }

        public DurableCommitDispatcher(string name, IEventSource eventSource, 
            ICheckpointStore checkpointStore, TimeSpan checkpointStorageInterval,
            IDispatchCommits commitDispatcher)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException("name");
            }

            this.name = name;
            this.eventSource = eventSource;
            this.checkpointStore = checkpointStore;
            this.commitDispatcher = commitDispatcher;
            this.checkpointStorageInterval = checkpointStorageInterval;
        }

        /// <summary>
        /// Gets the name of the dispatcher.
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// Gets an observable of <see cref="Transaction"/> as they have been projected. When subscribers
        /// observe an exception it indicates that dispatching a commit has failed. This would probably
        /// indicate a serious issue where you may wish to consider terminiating your application.
        /// </summary>
        public IObservable<Transaction> DispatchedCommits
        {
            get { return dispatchedCommits; }
        }

        /// <summary>
        /// Returns the checkpoint token of the most recent commit processed by the current dispatcher, or an empty string
        /// if the dispatcher has not processed any commits yet.
        /// </summary>
        public string LastProcessedCheckpoint
        {
            get { return lastProcessedCheckpoint ?? ""; }
        }

        /// <summary>
        /// Starts observing commits and dispatching them..
        /// </summary>
        /// <returns></returns>
        public async Task Start()
        {
            if (isStarted.EnsureCalledOnce())
            {
                return;
            }

            lastProcessedCheckpoint = await checkpointStore.Get();
            subscription = eventSource.Subscribe(lastProcessedCheckpoint, HandleCommit);

            checkpointStoreSubscription = dispatchedCommits
                .Sample(checkpointStorageInterval)
                .Select(_ => Observable.FromAsync(StoreCheckpoint))
                .Concat()
                .Subscribe(_ => { }, _ =>
                {
                    /*
                      * Error occurred, avoid default behaviour that would just 
                      * throw and skip other subscribers from receiving it.
                      * */
                });

            Logger.InfoFormat("{0} started at checkpoint {1}", name, lastProcessedCheckpoint ?? "(none)");
        }

        private async Task StoreCheckpoint()
        {
            try
            {
                Logger.Debug("Updating checkpoint store with " + LastProcessedCheckpoint);
                await checkpointStore.Put(lastProcessedCheckpoint);
                Logger.Info("Updated checkpoint store with " + LastProcessedCheckpoint);
            }
            catch (Exception ex)
            {
                Logger.ErrorException(
                    string.Format("Exception has occurred when storing the checkpoint {0} by {1}",
                        lastProcessedCheckpoint, name), ex);
            }
        }

        private async Task HandleCommit(Transaction eventTransaction)
        {
            try
            {
                await commitDispatcher.Dispatch(eventTransaction, disposed.Token);
                lastProcessedCheckpoint = eventTransaction.Checkpoint;
            }
            catch (Exception ex)
            {
                Logger.WarnException(
                    string.Format("Exception has occurred when dispatching a commit {0}.", eventTransaction), ex);

                dispatchedCommits.OnError(ex);
                throw;
            }

            dispatchedCommits.OnNext(eventTransaction);
        }

        /// <summary>
        /// Polls the EventStore for new commits. Invoking this from a NEventStore pipeline will help to reduce latency when
        /// dispatching commits to handlers.
        /// </summary>
        public void PollNow()
        {
            if (eventSource != null)
            {
                eventSource.RetrieveNow();
            }
        }

        /// <summary>
        /// Returns a task that completes when the dispatcher has dispatched the commit identified by the specifed 
        /// <paramref name="checkPointToken"/>, when the <paramref name="timeout"/> has expired or when 
        /// cancellation has been requested through the <paramref name="cancellationToken"/>.
        /// </summary>
        public async Task<bool> WaitForDispatchOf(string checkPointToken, TimeSpan timeout,
            CancellationToken cancellationToken = new CancellationToken())
        {
            if (!isStarted.Value)
            {
                throw new InvalidOperationException("The dispatcher hasn't started yet");
            }

            if (HasReachedCheckpoint(checkPointToken, LastProcessedCheckpoint))
            {
                return true;
            }

            Task<Transaction> dispatchingTask = dispatchedCommits
                .Where(c => HasReachedCheckpoint(checkPointToken, c.Checkpoint))
                .Take(1)
                .ToTask(cancellationToken);

            if (HasReachedCheckpoint(checkPointToken, LastProcessedCheckpoint))
            {
                return true;
            }

            Task completionTask = await Task
                .WhenAny(dispatchingTask, Task.Delay(timeout, cancellationToken))
                .ConfigureAwait(false);

            return (completionTask == dispatchingTask) && !completionTask.IsCanceled;
        }

        private bool HasReachedCheckpoint(string expectedCheckPoint, string actualCheckpoint)
        {
            return eventSource.CompareCheckpoints(actualCheckpoint, expectedCheckPoint) >= 0;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref isDisposed, 1, 0) == 1)
            {
                return;
            }

            disposed.Cancel();
            dispatchedCommits.Dispose();

            if (subscription != null)
            {
                subscription.Dispose();
            }

            if (checkpointStoreSubscription != null)
            {
                checkpointStoreSubscription.Dispose();
            }
        }

        public override string ToString()
        {
            return string.Format("{{name:{0}, checkpoint: {1}}}", Name, lastProcessedCheckpoint ?? "(none)");
        }
    }
}