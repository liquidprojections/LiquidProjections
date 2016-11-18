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
            Task completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));

            if (completedTask == task)
            {
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
