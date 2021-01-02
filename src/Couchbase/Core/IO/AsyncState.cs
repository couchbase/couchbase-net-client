using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO
{
    /// <summary>
    /// Represents an asynchronous Memcached request in flight.
    /// </summary>
    internal class AsyncState
    {
        private static readonly Action<object?> SendResponseInternalAction = SendResponseInternal;

        // CompletionTask is rarely used, only to support graceful connection shutdown during pool scaling
        // So we delay initialization until it is requested.
        private volatile TaskCompletionSource<bool>? _tcs;

        // Used to track the completion state when there is not _tcs, so if it's requested later we can still
        // mark the task as complete before returning. This also helps with thread sync.
        private volatile int _isCompleted;

        public IPEndPoint? EndPoint { get; set; }
        public IOperation Operation { get; set; }
        public uint Opaque => Operation.Opaque;
        public Timer? Timer { get; set; }
        public ulong ConnectionId { get; set; }
        public ErrorMap? ErrorMap { get; set; }
        public string? LocalEndpoint { get; set; }

        /// <summary>
        /// Temporary storage for response data used by SendResponse. This avoids closure related heap allocations.
        /// </summary>
        private SlicedMemoryOwner<byte> _response;

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
                var newTcs = new TaskCompletionSource<bool>();
                Interlocked.CompareExchange(ref _tcs, newTcs, null);

                if (_isCompleted == 1)
                {
                    // Just in case we were completing at the same time we were creating the _tcs
                    _tcs.TrySetResult(true);
                }

                return _tcs.Task;
            }
        }

        public AsyncState(IOperation operation)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (operation == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(operation));
            }

            Operation = operation;
        }

        /// <summary>
        /// Cancels the current Memcached request that is in-flight.
        /// </summary>
        public void Cancel(ResponseStatus status)
        {
            var prevCompleted = Interlocked.Exchange(ref _isCompleted, 1);
            if (prevCompleted == 1)
            {
                // Operation is already completed
                return;
            }

            Timer?.Dispose();
            Timer = null;

            var response = BuildErrorResponse(Opaque, status);

            SendResponse(in response);

            _tcs?.TrySetCanceled();
        }

        public void Complete(in SlicedMemoryOwner<byte> response)
        {
            var prevCompleted = Interlocked.Exchange(ref _isCompleted, 1);
            if (prevCompleted == 1)
            {
                // Operation is already completed
                response.Dispose();
                return;
            }

            Timer?.Dispose();
            Timer = null;

            if (response.IsEmpty)
            {
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

            Timer?.Dispose();
            Timer = null;
        }

        internal static SlicedMemoryOwner<byte> BuildErrorResponse(uint opaque, ResponseStatus status)
        {
            var response = MemoryPool<byte>.Shared.RentAndSlice(24);
            var responseSpan = response.Memory.Span;

            ByteConverter.FromUInt32(opaque, responseSpan.Slice(HeaderOffsets.Opaque));
            ByteConverter.FromInt16((short) status, responseSpan.Slice(HeaderOffsets.Status));

            return response;
        }

        private void SendResponse(in SlicedMemoryOwner<byte> response)
        {
            _response = response;

            // We don't need the execution context to flow to callback execution
            // so we can reduce heap allocations by not flowing.
            using (ExecutionContext.SuppressFlow())
            {
                // Run callback in a new task to avoid blocking the connection read process
                Task.Factory.StartNew(SendResponseInternalAction, this);
            }
        }

        /// <summary>
        /// Used by SendResponse, using a static action reduces heap allocations.
        /// </summary>
        private static void SendResponseInternal(object? response)
        {
            var state = (AsyncState) response!;

            try
            {
                state.Operation.HandleOperationCompleted(in state._response);
            }
            catch
            {
                // Cleanup data on error
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
