using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Utils;

namespace Couchbase.UnitTests.Utils
{
    internal class FakeOperation : IOperation
    {
        private TaskCompletionSource<ResponseStatus> _tcs = new TaskCompletionSource<ResponseStatus>();

        public ValueTask<ResponseStatus> Completed => new ValueTask<ResponseStatus>(_tcs.Task);

        public void Reset()
        {
            _tcs = new TaskCompletionSource<ResponseStatus>();
        }

        public void HandleOperationCompleted(in SlicedMemoryOwner<byte> data)
        {
            var status = (ResponseStatus) ByteConverter.ToInt16(data.Memory.Span.Slice(HeaderOffsets.Status));
            _tcs.TrySetResult(status);
        }

        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            return _tcs.TrySetCanceled(cancellationToken);
        }

        public bool TrySetException(Exception ex)
        {
            return _tcs.TrySetException(ex);
        }

        public void Dispose()
        {
        }

        private System.Diagnostics.Stopwatch _operationAge = System.Diagnostics.Stopwatch.StartNew();
        public TimeSpan Elapsed => _operationAge.Elapsed;

        public uint Attempts { get; set; }
        public bool Idempotent => IsReadOnly;
        public List<RetryReason> RetryReasons { get; set; }
        public IRetryStrategy RetryStrategy { get; set; }
        public TimeSpan Timeout { get; set; }
        public CancellationToken Token
        {
            get => TokenPair;
            set => throw new NotImplementedException();
        }

        public CancellationTokenPair TokenPair { get; set; }
        public string ClientContextId { get; set; }
        public string Statement { get; set; }
        public bool PreserveTtl { get; }
        public OpCode OpCode { get; }
        public string Key { get; }
        public uint Opaque { get; }
        public ulong Cas { get; set; }
        public short? ReplicaIdx { get; set; }
        public uint? Cid { get; set; }
        public short? VBucketId { get; set; }
        public bool RequiresVBucketId { get; } = true;
        public Exception Exception { get; set; }
        public string CName { get; set; }
        public string SName { get; set; }
        public ReadOnlyMemory<byte> Data { get; }
        public string BucketName { get; set; }
        public IPEndPoint CurrentHost { get; set; }
        public OperationHeader Header { get; set; }
        public IRequestSpan Span { get; set; }
        public IValueRecorder Recorder { get; set; }

        public DateTime CreationTime { get; set; }

        public Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            IsSent = true;
            return Task.CompletedTask;
        }

        public BucketConfig ReadConfig(ITypeTranscoder transcoder)
        {
            throw new NotImplementedException();
        }

        public bool WasNmvb()
        {
            throw new NotImplementedException();
        }

        public long? LastServerDuration { get; }

        public void LogOrphaned()
        {
        }

        public void StopRecording()
        {
        }

        public bool IsReadOnly { get; set; }
        public bool IsSent { get; private set; }

        public bool CanRetry() => true;

        public IOperationResult GetResult()
        {
            throw new NotImplementedException();
        }

        public IOperation Clone()
        {
            return new FakeOperation();
        }

        public SlicedMemoryOwner<byte> ExtractBody()
        {
            throw new NotImplementedException();
        }

        public bool HasDurability { get; set; }

        public string LastDispatchedFrom => throw new NotImplementedException();

        public string LastDispatchedTo => throw new NotImplementedException();
    }
}
