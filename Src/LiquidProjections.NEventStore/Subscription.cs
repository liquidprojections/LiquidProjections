using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidProjections.NEventStore
{
    internal sealed class Subscription : IDisposable
    {
        private readonly NEventStoreAdapter eventStoreClient;
        private CancellationTokenSource cancellationTokenSource;
        private readonly object syncRoot = new object();
        private bool isDisposed;
        private long? lastCheckpoint;
        private readonly IObserver<IReadOnlyList<Transaction>> observer;
        private volatile bool hasFailed;

        public Subscription(NEventStoreAdapter eventStoreClient, long? checkpoint,
            IObserver<IReadOnlyList<Transaction>> observer)
        {
            this.eventStoreClient = eventStoreClient;
            lastCheckpoint = checkpoint;
            this.observer = observer;
        }

        public Task Task { get; private set; }

        public void Start()
        {
            if (Task != null)
            {
                throw new InvalidOperationException("Already started.");
            }

            lock (syncRoot)
            {
                cancellationTokenSource = new CancellationTokenSource();

                Task = Task.Factory
                    .StartNew(
                        RunAsync,
                        cancellationTokenSource.Token,
                        TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                        TaskScheduler.Default)
                    .Unwrap();
            }
        }

        public void Complete()
        {
            Dispose();

            if (!hasFailed)
            {
                observer.OnCompleted();
            }
        }

        public void Dispose()
        {
            bool isDisposing;

            lock (syncRoot)
            {
                isDisposing = !isDisposed;

                if (isDisposing)
                {
                    isDisposed = true;
                }
            }

            if (isDisposing)
            {
                if (cancellationTokenSource != null)
                {
                    if (!cancellationTokenSource.IsCancellationRequested)
                    {
                        cancellationTokenSource.Cancel();
                    }

                    Task?.Wait();
                    cancellationTokenSource.Dispose();
                }

                lock (eventStoreClient.subscriptionLock)
                {
                    eventStoreClient.subscriptions.Remove(this);
                }
            }
        }

        private async Task RunAsync()
        {
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    var page = await eventStoreClient.GetNextPage(lastCheckpoint);
                    observer.OnNext(page.Transactions);
                    lastCheckpoint = page.LastCheckpoint;
                }
            }
            catch (Exception exception)
            {
                hasFailed = true;
                observer.OnError(exception);
                throw;
            }
        }
    }
}