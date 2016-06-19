using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using eVision.QueryHost.Logging;

namespace eVision.QueryHost.Dispatching
{
    public class ExceptionRetryPolicy
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly TimeSpan duration;
        private readonly TimeSpan retryInterval;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionRetryPolicy"/> class.
        /// </summary>
        /// <param name="retryInterval">The retry interval.</param>
        /// <param name="duration">The duration.</param>
        /// <exception cref="System.ArgumentException">
        /// retryInterval
        /// or
        /// duration
        /// </exception>
        public ExceptionRetryPolicy(TimeSpan retryInterval, TimeSpan duration)
        {
            if (retryInterval.Ticks < 0)
            {
                throw new ArgumentException("Retry interval should not be negative.", "retryInterval");
            }

            if (duration.Ticks < 0)
            {
                throw new ArgumentException("Retry duration should not be negative.", "duration");
            }

            this.retryInterval = retryInterval;
            this.duration = duration;
        }

        public static ExceptionRetryPolicy Indefinite(TimeSpan retryInterval)
        {
            return new ExceptionRetryPolicy(retryInterval, TimeSpan.MaxValue);
        }

        public static ExceptionRetryPolicy None()
        {
            return new ExceptionRetryPolicy(TimeSpan.Zero, TimeSpan.Zero);
        }

        public async Task Retry(Func<Task> operation, CancellationToken ct)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            int attemptCount = 0;
            while (true)
            {
                Exception exception = null;
                try
                {
                    await operation();
                }
                catch (Exception ex)
                {
                    Logger.WarnException(string.Format("Exception occured. Attempts: {0}", attemptCount), ex);
                    exception = ex;
                }
                if (exception == null)
                {
                    break;
                }
                if (stopwatch.Elapsed < duration)
                {
                    await Task.Delay(retryInterval, ct);
                    attemptCount++;
                    continue;
                }
                Logger.ErrorException(string.Format("Exception excided duration. Attempts: {0}; duration: {1}", attemptCount, duration), exception);
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }
    }
}