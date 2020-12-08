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
        // CompletionTask is rarely used, only to support graceful connection shutdown during pool scaling
        // So we delay initialization until it is requested.
        private volatile TaskCompletionSource<bool>? _tcs;

        // Used to track the completion state when there is not _tcs, so if it's requested later we can still
        // mark the task as complete before returning. This also helps with thread sync.
        private volatile bool _isCompleted;

        public IPEndPoint? EndPoint { get; set; }
        public Action<IMemoryOwner<byte>, ResponseStatus> Callback { get; set; }
        public uint Opaque { get; }
        public Timer? Timer { get; set; }
        public ulong ConnectionId { get; set; }
        public ErrorMap? ErrorMap { get; set; }
        public string? LocalEndpoint { get; set; }

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

                if (_isCompleted)
                {
                    // Just in case we were completing at the same time we were creating the _tcs
                    _tcs.TrySetResult(true);
                }

                return _tcs.Task;
            }
        }

        public AsyncState(Action<IMemoryOwner<byte>, ResponseStatus> callback, uint opaque)
        {
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            Opaque = opaque;
        }

        /// <summary>
        /// Cancels the current Memcached request that is in-flight.
        /// </summary>
        public void Cancel(ResponseStatus status)
        {
            Timer?.Dispose();
            Timer = null;

            var response = MemoryPool<byte>.Shared.RentAndSlice(24);
            ByteConverter.FromUInt32(Opaque, response.Memory.Span.Slice(HeaderOffsets.Opaque));

            Callback(response, status);

            _isCompleted = true;
            _tcs?.TrySetCanceled();
        }

        public void Complete(IMemoryOwner<byte>? response)
        {
            Timer?.Dispose();
            Timer = null;

            ResponseStatus status;

            if (response == null)
            {
                //this means the request never completed - assume a transport failure
                response = MemoryPool<byte>.Shared.RentAndSlice(24);
                ByteConverter.FromUInt32(Opaque, response.Memory.Span.Slice(HeaderOffsets.Opaque));
                status = ResponseStatus.TransportFailure;
            }
            else
            {
                //defaults
                status = (ResponseStatus) ByteConverter.ToInt16(response.Memory.Span.Slice(HeaderOffsets.Status));
            }

            // We don't need the execution context to flow to callback execution
            // so we can reduce heap allocations by not flowing.
            using (ExecutionContext.SuppressFlow())
            {
                // Run callback in a new task to avoid blocking the connection read process
                Task.Factory.StartNew(() => Callback(response, status));
            }

            _isCompleted = true;
            _tcs?.TrySetResult(true);
        }

        public void Dispose()
        {
            Timer?.Dispose();
            Timer = null;
        }
    }
}
