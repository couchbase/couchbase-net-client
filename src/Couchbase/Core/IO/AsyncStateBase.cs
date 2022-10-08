using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO
{
    internal class AsyncStateBase : IDisposable
    {
        // CompletionTask is rarely used, only to support graceful connection shutdown during pool scaling
        // So we delay initialization until it is requested.
        protected volatile TaskCompletionSource<bool>? _tcs;

        // Used to track the completion state when there is not _tcs, so if it's requested later we can still
        // mark the task as complete before returning. This also helps with thread sync.
        protected volatile int _isCompleted;

        private readonly LightweightStopwatch _stopwatch;

        public AsyncStateBase(IOperation operation)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (operation == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(operation));
            }

            Operation = operation;
            _stopwatch = LightweightStopwatch.StartNew();
        }


        public EndPoint? EndPoint { get; set; }
        public IOperation Operation { get; set; }
        public uint Opaque => Operation.Opaque;
        public ulong ConnectionId { get; set; }
        public string? LocalEndpoint { get; set; }

        public TimeSpan TimeInFlight => _stopwatch.Elapsed;

        /// <summary>
        /// Temporary storage for response data used by SendResponse. This avoids closure related heap allocations.
        /// </summary>
        internal SlicedMemoryOwner<byte> _response;

        public Task CompletionTask
        {
            get
            {
                if (_tcs != null)
                {
                    return _tcs.Task;
                }

                // This may create an extra TCS if two callers hit CompletionTask at the same time
                // But that's unlikely so worth keeping the cost low with Interlocked.CompareExchange
                var newTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Interlocked.CompareExchange(ref _tcs, newTcs, null);

                if (_isCompleted == 1)
                {
                    // Just in case we were completing at the same time we were creating the _tcs
                    _tcs.TrySetResult(true);
                }

                return _tcs.Task;
            }
        }

        public virtual void Complete(in SlicedMemoryOwner<byte> response)
        {
            var prevCompleted = Interlocked.Exchange(ref _isCompleted, 1);
            if (prevCompleted == 1)
            {
                // Operation is already completed
                response.Dispose();
                return;
            }

            if (response.IsEmpty)
            {
                Operation.LogOrphaned();
                //this means the request never completed - assume a transport failure
                SendResponse(BuildErrorResponse(Opaque, ResponseStatus.TransportFailure));
            }
            else
            {
                SendResponse(in response);
            }

            _tcs?.TrySetResult(true);
        }

        public void Dispose()
        {
            // Note: Don't dispose _response, this needs to live until the SendResponse task is executed.
            // The SendResponse task passes dispose responsibility forward to the operation.

            _isCompleted = 1;
            _tcs?.TrySetCanceled();
        }

        internal static SlicedMemoryOwner<byte> BuildErrorResponse(uint opaque, ResponseStatus status)
        {
            var response = MemoryPool<byte>.Shared.RentAndSlice(24);
            var responseSpan = response.Memory.Span;

            ByteConverter.FromUInt32(opaque, responseSpan.Slice(HeaderOffsets.Opaque));
            ByteConverter.FromInt16((short)status, responseSpan.Slice(HeaderOffsets.Status));

            return response;
        }

#if NETCOREAPP3_1_OR_GREATER
        private static readonly Action<object?> SendResponseInternalAction = SendResponseInternal;

        protected void SendResponse(in SlicedMemoryOwner<byte> response)
        {
            _response = response;

            // Queue the request on the global queue, but don't capture the current ExecutionContext
            ThreadPool.UnsafeQueueUserWorkItem(SendResponseInternalAction, this, preferLocal: false);
        }
#else
        protected static readonly WaitCallback SendResponseInternalCallback = SendResponseInternal;

        protected void SendResponse(in SlicedMemoryOwner<byte> response)
        {
            _response = response;

            // Queue the request on the global queue, but don't capture the current ExecutionContext
            ThreadPool.UnsafeQueueUserWorkItem(SendResponseInternalCallback, this);
        }
#endif

        /// <summary>
        /// Used by SendResponse, using a static action reduces heap allocations.
        /// </summary>
        protected static void SendResponseInternal(object? response)
        {
            var state = (AsyncStateBase)response!;

            try
            {
                //TODO: OperationBase error leads here
                state.Operation.HandleOperationCompleted(in state._response);
            }
            catch
            {
                state._response.Dispose();
                throw;
            }
            finally
            {
                state._response = default;
            }
        }
    }
}
