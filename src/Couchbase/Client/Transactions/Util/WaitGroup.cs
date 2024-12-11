using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace Couchbase.Client.Transactions.Util;

/// <summary>
/// An implementation of a WaitGroup, similar to Go, suitable for the purposes of ExtThreadSafety.
/// Operations can be added to the WaitGroup, and they remove themselves when finished.
/// Caller can also use TryWhenAll to wait for remaining items to finish.
/// </summary>
/// <see>https://hackmd.io/bJzZMt3ASfe8b7309lzK1A#ExtThreadSafety</see>
internal class WaitGroup
{
    private readonly ConcurrentDictionary<string, Waiter> _waiters = new();
    private long _runningTotal = 0;
    private IEnumerable<Task> Tasks => _waiters.Values.Select(w => w.Task);

    public async Task<bool> TryWhenAll(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
        {
            return false;
        }
        var allCompleted = Task.WhenAll(Tasks);
        var delay = Task.Delay(timeout);
        var any = await Task.WhenAny(allCompleted, delay).CAF();
        return allCompleted.IsCompleted;
    }

    public Waiter Add(string dbg)
    {
        string operationId = Guid.NewGuid().ToString();
        var waiter = _waiters.AddOrUpdate(operationId,
            addValueFactory: key =>
            {
                Interlocked.Increment(ref _runningTotal);
                return new Waiter(dbg, key);
            },
            updateValueFactory: (key, oldWaiter) => new Waiter(dbg, key));
        return waiter;
    }
    public bool TryRemoveOp(string operationId)
    {
        if(_waiters.TryRemove(operationId, out var removed))
        {
            removed.Dispose();
            return true;
        }
        return false;
    }
    public long RunningTotal => Interlocked.Read(ref _runningTotal);
    public int CurrentCount => _waiters.Count;
    public override string ToString()
    {
        return String.Join(",", _waiters);
    }

    internal class Waiter : IDisposable
    {
        private readonly string _dbg;
        private readonly TaskCompletionSource<string> _tcs;
        private readonly Stopwatch _age = Stopwatch.StartNew();
        public string OperationId { get; }
        public Waiter(string dbg, string operationId)
        {
            OperationId = operationId;
            _dbg = dbg;
            _tcs = new TaskCompletionSource<string>();
        }
        public Task Task => _tcs.Task;
        public void Dispose()
        {
            _tcs.TrySetResult(_dbg);
        }
        public override string ToString()
        {
            return $"{_dbg} {_tcs.Task.Status} ({_age.Elapsed.TotalMilliseconds:F2}ms";
        }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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
