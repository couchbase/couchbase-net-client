using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Utils;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes
{
    internal class FakeOperation : IOperation
    {
        private static Random _random = new Random();
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Couchbase.Core.Diagnostics.Tracing.IRequestSpan Span2 { get; set; }
        public uint Attempts { get; set; }
        public bool Idempotent { get; }
        public List<RetryReason> RetryReasons { get; set; }
        public IRetryStrategy RetryStrategy { get; set; }
        public TimeSpan Timeout { get; set; }
        public CancellationToken Token { get; set; }
        public string? ClientContextId { get; set; }
        public string? Statement { get; set; }
        public bool PreserveTtl { get; }
        public OpCode OpCode { get; }
        public string? BucketName { get; }
        public string? SName { get; }
        public string? CName { get; }
        public uint? Cid { get; set; }
        public string Key { get; }
        public bool RequiresVBucketId { get; }
        public short? VBucketId { get; set; }
        public short? ReplicaIdx { get; }
        public uint Opaque { get; }
        public ulong Cas { get; }
        public OperationHeader Header { get; }
        public IRequestSpan Span { get; }
        public IValueRecorder Recorder { get; set; }
        public bool HasDurability { get; }
        public bool IsReadOnly { get; }
        public bool IsSent { get; }
        public ValueTask<ResponseStatus> Completed { get; }
        public void Reset()
        {
            throw new NotImplementedException();
        }

        public async Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            connection.AddTags(Span2);

            var dispatch = _random.Next(200, 1000);
            using var dispatchSpan = Span2.EncodingSpan();
            //await Task.Delay(dispatch);

            using var encodingSpan = dispatchSpan.DispatchSpan(this);
            var encoding = _random.Next(200, 1000);
            //await Task.Delay(encoding);
        }

        public bool TrySetCanceled(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public bool TrySetException(Exception ex)
        {
            throw new NotImplementedException();
        }

        public void HandleOperationCompleted(in SlicedMemoryOwner<byte> data)
        {
            throw new NotImplementedException();
        }

        public SlicedMemoryOwner<byte> ExtractBody()
        {
            throw new NotImplementedException();
        }

        public BucketConfig? ReadConfig(ITypeTranscoder transcoder)
        {
            throw new NotImplementedException();
        }

        public void StopRecording()
        {
            throw new NotImplementedException();
        }
    }
}
