using System;
using System.Threading;
using System.Threading.Tasks;

namespace LiquidProjections.NEventStore
{
    internal static class TaskExtensions
    {
        public static Task<TResult> WithWaitCancellation<TResult>(this Task<TResult> task,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<TResult>();
            var registration = cancellationToken.Register(s =>
            {
                var source = (TaskCompletionSource<TResult>) s;
                source.TrySetCanceled();
            }, tcs);

            task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = (Tuple<TaskCompletionSource<TResult>, CancellationTokenRegistration>) s;

                if (t.IsFaulted && t.Exception!= null)
                {
                    tcsAndRegistration.Item1.TrySetException(t.Exception.InnerException);
                }

                if (t.IsCanceled)
                {
                    tcsAndRegistration.Item1.TrySetCanceled();
                }

                if (t.IsCompleted)
                {
                    tcsAndRegistration.Item1.TrySetResult(t.Result);
                }

                tcsAndRegistration.Item2.Dispose();
            }, Tuple.Create(tcs, registration), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            return tcs.Task;
        }
    }
}
