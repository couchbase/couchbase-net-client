using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.LoadTests.Helpers
{
    public static class RateLimiter
    {
        public static Task ExecuteRateLimited<T>(this IEnumerable<T> source, Func<T, Task> action, int rateLimit)
        {
            return source.ExecuteRateLimited(action, rateLimit, CancellationToken.None);
        }

        public static async Task ExecuteRateLimited<T>(this IEnumerable<T> source, Func<T, Task> action, int rateLimit, CancellationToken cancellationToken)
        {
            using (var semaphore = new SemaphoreSlim(rateLimit))
            {
                var tasks = new List<Task>();

                foreach (var item in source)
                {
                    await semaphore.WaitAsync(cancellationToken);

                    var task = action.Invoke(item)
                        // ReSharper disable once AccessToDisposedClosure
                        .ContinueWith(t2 => semaphore.Release(), cancellationToken);

                    tasks.Add(task);
                }

                await Task.WhenAll(tasks);
            }
        }
    }
}
