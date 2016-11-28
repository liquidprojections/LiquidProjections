using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidProjections.NEventStore
{
    internal static class TaskExtensions
    {
        public static async Task<TResult> WithWaitCancellation<TResult>(this Task<TResult> task,
            CancellationToken cancellationToken)
        {
            using (var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                var delayTask = Task.Delay(Timeout.Infinite, combined.Token);
                Task completedTask = await Task.WhenAny(task, delayTask);
                if (completedTask == task)
                {
                    combined.Cancel();
                    return await task;
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Infinite delay task completed.");
                }
            }
        }
    }
}
