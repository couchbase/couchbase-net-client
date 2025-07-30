#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Core.Exceptions.Analytics;

namespace Couchbase.Client.Transactions.Components;

// this is a simple way to limit the number of parallel tasks to some maximum.   TODO: add
// construct for cancelation of pending tasks.  This is an optimization so lets add it later
// after the first pass is working.
internal class TaskLimiter(int maxParallelTasks)
{
    private readonly SemaphoreSlim _semaphore = new(initialCount: maxParallelTasks);
    private readonly List<Task> _tasks = [];

    // call this, which adds the task to the set of tasks we run.
    public void Run<TInput>(TInput item, Func<TInput, Task> handler)
    {
        var task = Task.Run(async () =>
        {
            await _semaphore.WaitAsync().CAF();
            try
            {
                await handler(item).CAF();
            }
            finally
            {
                _semaphore.Release();
            }
        });
        _tasks.Add(task);
    }

    public void Run<TInput, TResult>(TInput item, int originalIndex,
        Func<TInput, Task<TResult>> handler, Action<TResult, int> handleResult)
    {
        var task = Task.Run(async () =>
        {
            await _semaphore.WaitAsync().CAF();
            try
            {
                var result = await handler(item).CAF();
                handleResult(result, originalIndex);
            }
            finally
            {
                _semaphore.Release();
            }
        });
        _tasks.Add(task);
    }

    // wait for all to be complete
    public async Task WaitAllAsync()
    {
        await Task.WhenAll(_tasks).CAF();
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
