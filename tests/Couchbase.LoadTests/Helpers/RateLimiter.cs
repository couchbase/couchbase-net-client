using System;
using System.Collections.Concurrent;
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
                var tasks = new ConcurrentDictionary<int, Task>();
                var i = 0;

                foreach (var item in source)
                {
                    await semaphore.WaitAsync(cancellationToken);

                    var task = Task.Run(() => action.Invoke(item), cancellationToken);
                    tasks.TryAdd(i, task);

                    // ReSharper disable once AccessToDisposedClosure
#pragma warning disable 4014
                    task.ContinueWith((t2, state) =>
                    {
                        var j = (int) state;
                        semaphore.Release();
                        tasks.TryRemove(j, out _);
                    }, i, cancellationToken);
#pragma warning restore 4014

                    i++;
                }

                await Task.WhenAll(tasks.Values);
            }
        }
    }
}
