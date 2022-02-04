using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Test.Common.Utils
{
    /// <summary>
    /// General util for retrying operations on resources that may be provisioned by the server yet.
    /// </summary>
    /// <remarks>https://stackoverflow.com/questions/1563191/cleanest-way-to-write-retry-logic</remarks>
    public static class Retry
    {
        public static async Task DoAsync(
            Func<Task> action,
            TimeSpan retryInterval,
            int maxAttemptCount = 3)
        {
            var exceptions = new List<Exception>();

            for (var attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                try
                {
                    if (attempted > 0)
                    {
                        await Task.Delay(retryInterval);
                    }
                    await action();
                    return;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        public static void Do(
            Action action,
            TimeSpan retryInterval,
            int maxAttemptCount = 3)
        {
            Do<object>(() =>
            {
                action();
                return null;
            }, retryInterval, maxAttemptCount);
        }

        public static T Do<T>(
            Func<T> action,
            TimeSpan retryInterval,
            int maxAttemptCount = 3)
        {
            var exceptions = new List<Exception>();

            for (var attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                try
                {
                    if (attempted > 0)
                    {
                        Thread.Sleep(retryInterval);
                    }
                    return action();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
            throw new AggregateException(exceptions);
        }

        /// <summary>
        /// Retry a boolean func with exponential backoff until it returns true, or the max attempts is reached
        /// </summary>
        public static async Task DoUntilAsync(
            Func<bool> supplier,
            int retryIntervalMillis = 10,
            int maxRetryIntervalMillis = 1000,
            int maxAttemptCount = 32)
        {
            var exceptions = new List<Exception>();
            var pow = 1;

            for (var attempted = 0; attempted < maxAttemptCount; attempted++)
            {
                try
                {
                    if (attempted < 31)
                    {
                        pow <<= 1;
                    }
                    var delay = Math.Min(retryIntervalMillis * (pow - 1) / 2,
                        maxRetryIntervalMillis);
                    await Task.Delay(delay);

                    if (supplier())
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
            throw new AggregateException($"Supplier did not return true in {maxAttemptCount} attempts.", exceptions);
        }
    }
}
