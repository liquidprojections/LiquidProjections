using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using eVision.QueryHost.Common;
using eVision.QueryHost.NEventStore.Logging;
using NEventStore;
using NEventStore.Persistence;

namespace eVision.QueryHost.NEventStore
{
    // Imported from NES 6, will be removed when NES6 is out (copied from Cedar)
    public class EventStoreClient : IEventSource, IDisposable
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

        public const int DefaultPollingInterval = 5000;
        private readonly IPersistStreams persistStreams;
        private readonly int pageSize;

        private readonly ConcurrentDictionary<Guid, Subscriber> subscribers =
            new ConcurrentDictionary<Guid, Subscriber>();

        private readonly InterlockedBoolean isRetrieving = new InterlockedBoolean();
        private readonly IDisposable retrieveTimer;
        private readonly LruCache<string, ICommit[]> commitsCache = new LruCache<string, ICommit[]>(100);

        public EventStoreClient(IPersistStreams persistStreams, int pollingIntervalMilliseconds = DefaultPollingInterval,
            int pageSize = 1)
        {
            this.persistStreams = persistStreams;
            this.pageSize = pageSize;
            retrieveTimer = Observable
                .Interval(TimeSpan.FromMilliseconds(pollingIntervalMilliseconds))
                .Subscribe(_ => RetrieveNow());
        }

        public void Dispose()
        {
            retrieveTimer.Dispose();
        }

        public IDisposable Subscribe(string fromCheckpoint, Func<Transaction, Task> onCommit)
        {
            var subscriberId = Guid.NewGuid();
            var subscriber = new Subscriber(fromCheckpoint, onCommit, pageSize, RetrieveNow, () =>
            {
                Subscriber _;
                subscribers.TryRemove(subscriberId, out _);
            });

            subscribers.TryAdd(subscriberId, subscriber);
            RetrieveNow();
            return subscriber;
        }

        public void RetrieveNow()
        {
            if (isRetrieving.CompareExchange(true, false))
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    RetrieveNowUnsafely();
                }
                catch (Exception ex)
                {
                    Logger.WarnException("Retrieving and dispatching commits failed: ", ex);
                    throw;
                }
                finally
                {
                    isRetrieving.Set(false);
                }
            });
        }

        private void RetrieveNowUnsafely()
        {
            foreach (var subscriber in subscribers.Values.ToArray())
            {
                if (subscriber.QueueLength >= pageSize)
                {
                    continue;
                }

                string key = subscriber.Checkpoint ?? "<null>";
                ICommit[] commits;

                if (!commitsCache.TryGet(key, out commits))
                {
                    commits = GetPageOfCommits(subscriber.Checkpoint);
                    if (commits.Length == pageSize)
                    {
                        // Only store full page prevents
                        commitsCache.Set(key, commits);
                    }
                }

                foreach (ICommit commit in commits)
                {
                    subscriber.Enqueue(ConvertToTransaction(commit));
                }
            }
        }

        private Transaction ConvertToTransaction(ICommit commit)
        {
            return new Transaction
            {
                Id = commit.CommitId,
                StreamId = commit.StreamId,
                Checkpoint = commit.CheckpointToken,
                TimeStamp = commit.CommitStamp,
                Events = new List<Envelope>(commit.Events.Select(@event => new Envelope
                {
                    Body = @event.Body,
                    Headers = @event.Headers
                }))
            };
        }

        private ICommit[] GetPageOfCommits(string checkPoint)
        {
            try
            {
                return persistStreams.GetFrom(checkPoint)
                    .Take(pageSize)
                    .ToArray();
            }
            catch
            {
                Logger.Warn("Failed to retrieve " + pageSize + " commits as of checkpoint " + checkPoint);
                throw;
            }
        }

        public int CompareCheckpoints(string checkpointToken1, string checkpointToken2)
        {
            ICheckpoint checkpoint1 = persistStreams.GetCheckpoint(checkpointToken1);
            ICheckpoint checkpoint2 = persistStreams.GetCheckpoint(checkpointToken2);

            return checkpoint1.CompareTo(checkpoint2);
        }

        private class Subscriber : IDisposable
        {
            private readonly Func<Transaction, Task> onCommit;
            private readonly int threshold;
            private readonly Action onThreashold;
            private readonly Action onDispose;
            private readonly ConcurrentQueue<Transaction> commits = new ConcurrentQueue<Transaction>();
            private readonly InterlockedBoolean isPushing = new InterlockedBoolean();

            public Subscriber(
                string checkpoint,
                Func<Transaction, Task> onCommit,
                int threshold,
                Action onThreashold,
                Action onDispose)
            {
                Checkpoint = checkpoint;
                this.onCommit = onCommit;
                this.threshold = threshold;
                this.onThreashold = onThreashold;
                this.onDispose = onDispose;
            }

            public string Checkpoint { get; private set; }

            public void Enqueue(Transaction transaction)
            {
                commits.Enqueue(transaction);
                Checkpoint = transaction.Checkpoint;
                Push();
            }

            public int QueueLength
            {
                get { return commits.Count; }
            }

            public void Dispose()
            {
                onDispose();
            }

            private void Push()
            {
                if (isPushing.CompareExchange(true, false))
                {
                    return;
                }

                Task.Run(async () =>
                {
                    Transaction transaction;
                    while (commits.TryDequeue(out transaction))
                    {
                        try
                        {
                            await onCommit(transaction);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Error, () => "Exception occurred while pushing the commit: {0}", ex, transaction);
                        }

                        if (commits.Count < threshold)
                        {
                            onThreashold();
                        }
                    }

                    isPushing.Set(false);
                });
            }
        }
    }
}