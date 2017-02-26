using LiquidProjections.NEventStore.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private long previousCheckpoint;
        private readonly Func<IReadOnlyList<Transaction>, Task> observer;

        public Subscription(NEventStoreAdapter eventStoreClient, long previousCheckpoint,
            Func<IReadOnlyList<Transaction>, Task> observer, string subscriptionId = null)
        {
            this.eventStoreClient = eventStoreClient;
            this.previousCheckpoint = previousCheckpoint;
            this.observer = observer;
            Id = subscriptionId;
        }

        public Task Task { get; private set; }
        public string Id { get; }

        public void Start()
        {
            if (Task != null)
            {
                throw new InvalidOperationException("Already started.");
            }

            lock (syncRoot)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(Subscription));
                }

                cancellationTokenSource = new CancellationTokenSource();
#if DEBUG
                LogProvider.GetCurrentClassLogger().Debug(() => $"Subscription {Id ?? "without ID"} has been started.");
#endif

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
#if DEBUG
                    LogProvider.GetCurrentClassLogger().Debug(() => $"Subscription {Id ?? "without ID"} is being stopped.");
#endif

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

#if DEBUG
                LogProvider.GetCurrentClassLogger().Debug(() => $"Subscription {Id ?? "without ID"} has been stopped.");
#endif
            }
        }

        private async Task RunAsync()
        {
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
#if DEBUG
                    LogProvider.GetCurrentClassLogger().Debug(() =>
                        $"Subscription {Id ?? "without ID"} is requesting a page after checkpoint {previousCheckpoint}.");
#endif

                    Page page = await eventStoreClient.GetNextPage(previousCheckpoint
#if DEBUG
                        , Id
#endif
                        )
                        .WithWaitCancellation(cancellationTokenSource.Token)
                        .ConfigureAwait(false);

#if DEBUG
                    LogProvider.GetCurrentClassLogger().Debug(() =>
                        $"Subscription {Id ?? "without ID"} has got a page of size {page.Transactions.Count} " +
                        $"from checkpoint {page.Transactions.First().Checkpoint} "+
                        $"to checkpoint {page.Transactions.Last().Checkpoint}.");
#endif

                    await observer(page.Transactions).ConfigureAwait(false);

#if DEBUG
                    LogProvider.GetCurrentClassLogger().Debug(() =>
                        $"Subscription {Id ?? "without ID"} has processed a page of size {page.Transactions.Count} " +
                        $"from checkpoint {page.Transactions.First().Checkpoint} " +
                        $"to checkpoint {page.Transactions.Last().Checkpoint}.");
#endif

                    previousCheckpoint = page.LastCheckpoint;
                }
            }
            catch (OperationCanceledException)
            {
                // Do nothing.
            }
            catch (Exception exception)
            {
                LogProvider.GetCurrentClassLogger().FatalException(
                    "NEventStore polling task has failed. Event subscription has been cancelled.",
                    exception);
            }
        }
    }
}