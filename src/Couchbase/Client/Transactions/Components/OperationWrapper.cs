#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.External;
using Couchbase.Client.Transactions.Support;
using Couchbase.Core.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Components;

/// <summary>
/// This class wraps most transaction calls, allowing us to properly and consistently lock/unlock
/// as needed when commit, rollback, or switching over to queryMode.
/// </summary>
internal class OperationWrapper
{
    // Only for raising errors.   When we refactor AttemptContext to make more testable, we will only
    // store a reference to some other interface that AttemptContext implements.
    private readonly AttemptContext _attemptContext;

    // this starts life as false, and isn't true until we begin performing a transactional query
    private volatile bool _isQueryMode; // false is default value when initialized

    // number of operations that are "in flight"
    private int _operationsInFlight;  // 0 is default value when initialized

    private readonly object _lock = new object();
    private readonly SemaphoreSlim _queryLock = new(1, 1); // acts like mutex

    private volatile TaskCompletionSource<object?> _tasksCompleted = new();

    private volatile TaskCompletionSource<object?> _tasksCanStart = new();
    private readonly ILogger _logger;

    public bool IsQueryMode => _isQueryMode;

    public OperationWrapper(AttemptContext ctx, ILoggerFactory loggerFactory)
    {
        _attemptContext = ctx;
        _logger = loggerFactory.CreateLogger<OperationWrapper>();
        lock (_lock)
        {
            // start with this signaled, so we don't block anything (yet).
            _tasksCanStart.SetResult(null);
            // this too
            _tasksCompleted.SetResult(null);
        }
    }
    private async Task StartTaskAsync()
    {
        // we may disallow tasks temporarily
        _logger.LogDebug("Waiting for tasks to be able to start...");
        await _tasksCanStart.Task.CAF();
        _logger.LogDebug("Proceeding with tasks as we are able to start now.");

        // now lets increment the in flight operation count
        var newCount = Interlocked.Increment(ref _operationsInFlight);
        _logger.LogDebug("{newCount} Operations in flight", newCount);

        var builder = ErrorBuilder.CreateError(_attemptContext, ErrorClass.FailOther)
            .RaiseException(TransactionOperationFailedException.FinalError.TransactionFailed);
        switch (_attemptContext.AttemptState)
        {
            case AttemptStates.ABORTED:
            case AttemptStates.ROLLED_BACK:
                throw builder.Cause(new TransactionAlreadyAbortedException()).DoNotRollbackAttempt().Build();
            case AttemptStates.COMMITTED:
            case AttemptStates.COMPLETED:
                throw builder.Cause(new TransactionAlreadyCommittedException()).DoNotRollbackAttempt().Build();
            case AttemptStates.NOTHING_WRITTEN:
            case AttemptStates.PENDING:
                if (_attemptContext.StateFlags.IsFlagSet(StateFlags.BehaviorFlags.CommitNotAllowed))
                {
                    throw builder.Cause(new TransactionAlreadyCommittedException()).DoNotRollbackAttempt().Build();
                }
                break;
            default:
            {
                _logger?.LogDebug("AttemptState is unknown, proceeding...");
                break;
            }
        }
        // we are done now, unless we need to set the tcs for tasks completed
        if (newCount > 1) return;

        // We were at 0 and now are not, so create a new tcs
        lock (_lock)
        {
            _tasksCompleted = new TaskCompletionSource<object?>();
        }
    }

    private void EndTask()
    {
        var newCount = Interlocked.Decrement(ref _operationsInFlight);
        _logger.LogDebug("After decrementing, {newCount} operations in flight", newCount);
        if (newCount > 0) return;
        lock (_lock)
        {
            _tasksCompleted.TrySetResult(null);
        }
    }

    // This should make sure we do one query at a time.  We call the QueryWrapper function in AttemptContext
    // wrapped by this, so all query calls are similarly safe.   We probably just wrap the QueryWrapper
    // with this.  We should be able to safely call this within the WrapOperation call.
    public async Task<T> WrapQueryOperationAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            // We only do one query at a time so get the lock
            await _queryLock.WaitAsync().CAF();

            // if we are already in query mode, we can just proceed to call the operation and return
            if (_isQueryMode) return await operation().CAF();

            // Not in query mode so first lets temporarily block new operations...
            SetShouldBlockTaskStarting();

            // then wait for in-flight kv ops to complete...
            _logger.LogDebug("Waiting for in-flight operations to complete...");
            await _tasksCompleted.Task.CAF();
            try
            {
                // we up the op count here as we didn't come through the WrapOperation call and so it hasn't been
                // incremented yet.
                var newCount = Interlocked.Increment(ref _operationsInFlight);
                _logger.LogDebug("{newCount} tasks in flight (counting this one), continuing...", newCount);

                // ok now we can call the operation
                return await operation().CAF();
            } finally {
                // we have to decrement here as we incremented above.
                var newCount = Interlocked.Decrement(ref _operationsInFlight);
                _logger.LogDebug("Decrementing, now {newCount} operations in-flight", newCount);
            }
        } finally {
            _queryLock.Release();
        }
    }

    public async Task<T> WrapOperationAsync<T>(Func<Task<T>> kvOperation, Func<Task<T>> queryOperation)
    {
        try
        {
            await StartTaskAsync().CAF();
            if (_isQueryMode) return await queryOperation().CAF();
            return await kvOperation().CAF();
        }
        finally {
            EndTask();
        }
    }
    public async Task WrapOperationAsync(Func<Task> kvOperation, Func<Task> queryOperation)
    {
        try
        {
            await StartTaskAsync().CAF();
            if (_isQueryMode)
            {
                await queryOperation().CAF();
            } else{
                await kvOperation().CAF();
            }
        }
        finally {
            EndTask();
        }
    }

    public async Task WaitOnTasksThenPerformUnderLockAsync(Func<Task> operation)
    {
        try
        {
            SetShouldBlockTaskStarting();
            await WaitForTaskCompletion().CAF();
            _logger.LogDebug("Waiting for tasks completed...");
            await operation().CAF();
        } finally {
            ResetShouldBlockTaskStarting();
        }
    }

    public void SetQueryMode()
    {
        if (_isQueryMode) return;
        _logger.LogDebug("QueryMode set");
        _isQueryMode = true;
    }

    public void SetShouldBlockTaskStarting()
    {
        lock (_lock)
        {
            _tasksCanStart = new TaskCompletionSource<object?>();
        }
        _logger.LogDebug("New operations are blocked temporarily...");
    }
    public void ResetShouldBlockTaskStarting()
    {
       lock(_lock)
       {
           _tasksCanStart.TrySetResult(null);
           _logger.LogDebug("New Operations are unblocked");
       }
    }

    public async Task TasksCanStart()
    {
        await _tasksCanStart.Task.CAF();
    }

    public void ResetQueryMode()
    {
        _isQueryMode = false;
    }
    public Task WaitForTaskCompletion()
    {
        lock(_lock)
        {
            return _tasksCompleted.Task;
        }
    }

}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
