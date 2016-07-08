using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NEventStore;
using NEventStore.Persistence;

namespace LiquidProjections.NEventStore
{
    public class NEventStoreAdapter : IEventStore
    {
        private readonly TimeSpan pollInterval;
        private readonly int maxPageSize;
        private readonly IPersistStreams eventStore;
        internal readonly HashSet<Subscription> subscriptions = new HashSet<Subscription>();
        internal volatile bool isDisposed;
        internal readonly object subscriptionLock = new object();
        private Task<Page> currentLoader;
        private readonly LruCache<long, Transaction> transactionCache;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private CheckpointRequestTimestamp lastExistingCheckpointRequest;

        public NEventStoreAdapter(IPersistStreams eventStore, int cacheSize, TimeSpan pollInterval, int maxPageSize)
        {
            this.eventStore = eventStore;
            this.pollInterval = pollInterval;
            this.maxPageSize = maxPageSize;
            transactionCache = new LruCache<long, Transaction>(cacheSize);
        }

        public IObservable<IReadOnlyList<Transaction>> Subscribe(long? checkpoint)
        {
            return new PagesAfterCheckpoint(this, checkpoint);
        }

        internal async Task<Page> GetNextPage(long? checkpoint)
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(typeof(NEventStoreAdapter).FullName);
            }

            Page pageFromCache = TryGetNextPageFromCache(checkpoint ?? 0);
            if (pageFromCache.Transactions.Count > 0)
            {
                return pageFromCache;
            }

            Page loadedPage = await LoadNextPageSequentially(checkpoint);
            if (loadedPage.Transactions.Count == maxPageSize)
            {
                StartPreloadingNextPage(loadedPage.LastCheckpoint);
            }

            return loadedPage;
        }

        private Page TryGetNextPageFromCache(long checkpoint)
        {
            Transaction cachedNextTransaction;

            if (transactionCache.TryGet(checkpoint, out cachedNextTransaction))
            {
                var resultPage = new List<Transaction>(maxPageSize) {cachedNextTransaction};

                while (resultPage.Count < maxPageSize)
                {
                    long lastCheckpoint = cachedNextTransaction.Checkpoint;

                    if (transactionCache.TryGet(lastCheckpoint, out cachedNextTransaction))
                    {
                        resultPage.Add(cachedNextTransaction);
                    }
                    else
                    {
                        StartPreloadingNextPage(lastCheckpoint);
                        break;
                    }
                }

                return new Page(checkpoint, resultPage);
            }

            return new Page(checkpoint, new Transaction[0]);
        }

        private void StartPreloadingNextPage(long? checkpoint)
        {
            // Ignore result.
            Task _ = LoadNextPageSequentially(checkpoint);
        }

        private async Task<Page> LoadNextPageSequentially(long? checkpoint)
        {
            while (true)
            {
                if (isDisposed)
                {
                    return new Page(checkpoint, new Transaction[0]);
                }

                CheckpointRequestTimestamp effectiveLastExistingCheckpointRequest =
                    Volatile.Read(ref lastExistingCheckpointRequest);

                if ((effectiveLastExistingCheckpointRequest != null) &&
                    (effectiveLastExistingCheckpointRequest.Checkpoint == checkpoint))
                {
                    TimeSpan timeAfterPreviousRequest = DateTime.UtcNow - effectiveLastExistingCheckpointRequest.DateTimeUtc;

                    if (timeAfterPreviousRequest < pollInterval)
                    {
                        await Task.Delay(pollInterval - timeAfterPreviousRequest);
                    }
                }

                Page candidatePage = await TryLoadNextPageSequentiallyOrWaitForCurrentLoadingToFinish(checkpoint);
                if (candidatePage.PreviousCheckpoint == checkpoint && candidatePage.Transactions.Count > 0)
                {
                    return candidatePage;
                }
            }
        }

        private Task<Page> TryLoadNextPageSequentiallyOrWaitForCurrentLoadingToFinish(long? checkpoint)
        {
            TaskCompletionSource<Page> taskCompletionSource = null;
            bool isTaskOwner = false;

            try
            {
                Task<Page> loader = Volatile.Read(ref currentLoader);

                if (loader == null)
                {
                    taskCompletionSource = new TaskCompletionSource<Page>();
                    Task<Page> oldLoader = Interlocked.CompareExchange(ref currentLoader, taskCompletionSource.Task, null);
                    isTaskOwner = oldLoader == null;
                    loader = isTaskOwner ? taskCompletionSource.Task : oldLoader;

                    if (isDisposed)
                    {
                        taskCompletionSource = null;
                        isTaskOwner = false;
                        return Task.FromResult(new Page(checkpoint, new Transaction[0]));
                    }
                }

                return loader;
            }
            finally
            {
                if (isTaskOwner)
                {
                    // Ignore result.
                    Task _ = TryLoadNextPageAndMakeLoaderComplete(checkpoint, taskCompletionSource);
                }
            }
        }

        private  async Task TryLoadNextPageAndMakeLoaderComplete(long? checkpoint,
            TaskCompletionSource<Page> loaderCompletionSource)
        {
            Page nextPage;

            try
            {
                nextPage = await TryLoadNextPage(checkpoint);
            }
            catch (Exception exception)
            {
                loaderCompletionSource.SetException(exception);
                return;
            }
            finally
            {
                Volatile.Write(ref currentLoader, null);
            }

            loaderCompletionSource.SetResult(nextPage);
        }

        private async Task<Page> TryLoadNextPage(long? checkpoint)
        {
            // Maybe it's just loaded to cache.
            Page cachedPage = TryGetNextPageFromCache(checkpoint ?? 0);
            if (cachedPage.Transactions.Count > 0)
            {
                return cachedPage;
            }

            DateTime timeOfRequestUtc = DateTime.UtcNow;

            List<Transaction> transactions = await Task.Run(() =>
            {
                try
                {
                    return eventStore
                        .GetFrom((checkpoint != null) ? checkpoint.ToString() : "")
                        .Take(maxPageSize)
                        .Select(ToTransaction)
                        .ToList();

                }
                catch
                {
                    // TODO: Properly log the exception
                    return new List<Transaction>();
                }
            });

            if (transactions.Count > 0)
            {
                if (transactions.Count < maxPageSize)
                {
                    Volatile.Write(
                        ref lastExistingCheckpointRequest,
                        new CheckpointRequestTimestamp(transactions[transactions.Count - 1].Checkpoint, timeOfRequestUtc));
                }

                /* Add to cache in reverse order to prevent other projectors
                    from requesting already loaded transactions which are not added to cache yet. */
                for (int index = transactions.Count - 1; index > 0; index--)
                {
                    transactionCache.Set(transactions[index - 1].Checkpoint, transactions[index]);
                }

                transactionCache.Set(checkpoint ?? 0, transactions[0]);
            }

            return new Page(checkpoint, transactions);
        }

        private Transaction ToTransaction(ICommit commit)
        {
            return new Transaction
            {
                Id = commit.CommitId.ToString(),
                StreamId = commit.StreamId,

                // SMELL properly log an exception that we only support numeric based storage engines
                Checkpoint = long.Parse(commit.CheckpointToken),
                TimeStampUtc = commit.CommitStamp,
                Events = new List<EventEnvelope>(commit.Events.Select(@event => new EventEnvelope
                {
                    Body = @event.Body,
                    Headers = @event.Headers
                }))
            };
        }

        public void Dispose()
        {
            lock (subscriptionLock)
            {
                if (!isDisposed)
                {
                    isDisposed = true;

                    cancellationTokenSource.Cancel();

                    foreach (var subscription in subscriptions)
                    {
                        subscription.Complete();
                    }

                    Task loaderToWaitFor = Volatile.Read(ref currentLoader);
                    loaderToWaitFor?.Wait();

                    cancellationTokenSource.Dispose();
                    eventStore.Dispose();
                }
            }
        }
    }
}