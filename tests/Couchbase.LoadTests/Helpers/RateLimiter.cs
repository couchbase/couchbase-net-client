using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
            var tasks = Partitioner.Create(source)
                .GetPartitions(rateLimit)
                .Select(partition =>
                {
                    return Task.Run(async () =>
                    {
                        while (partition.MoveNext())
                        {
                            await action.Invoke(partition.Current);
                        }
                    }, cancellationToken);
                })
                .ToList();

            await Task.WhenAll(tasks);
        }
    }
}
