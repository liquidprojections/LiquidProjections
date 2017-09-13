using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidProjections.Testing
{
    internal static class TaskExtensions
    {
        public static Task WithWaitCancellation(this Task task, CancellationToken cancellationToken)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();
            CancellationTokenRegistration registration = cancellationToken.Register(CancelTask, taskCompletionSource);

            task.ContinueWith(ContinueTask, Tuple.Create(taskCompletionSource, registration), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return taskCompletionSource.Task;
        }

        private static void CancelTask(object state)
        {
            var taskCompletionSource = (TaskCompletionSource<bool>)state;
            taskCompletionSource.TrySetCanceled();
        }

        private static void ContinueTask(Task task, object state)
        {
            var tcsAndRegistration = (Tuple<TaskCompletionSource<bool>, CancellationTokenRegistration>)state;

            if (task.IsFaulted && (task.Exception != null))
            {
                tcsAndRegistration.Item1.TrySetException(task.Exception.InnerException);
            }

            if (task.IsCanceled)
            {
                tcsAndRegistration.Item1.TrySetCanceled();
            }

            if (task.IsCompleted)
            {
                tcsAndRegistration.Item1.TrySetResult(false);
            }

            tcsAndRegistration.Item2.Dispose();
        }
    }
}