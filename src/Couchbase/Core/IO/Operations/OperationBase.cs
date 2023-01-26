using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Core.Utils;
using Couchbase.Diagnostics;
using Couchbase.Utils;
using Microsoft.Extensions.ObjectPool;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <remarks>
    /// It is important for this object to be disposed. Failure to properly dispose this object may result
    /// in memory not being returned to the ArrayPool, which will increase GC impact across various parts of the framework.
    /// </remarks>
    internal abstract class OperationBase : IOperation, IValueTaskSource<ResponseStatus>
    {
        private SlicedMemoryOwner<byte> _data;
        private IRequestSpan? _span;
        private List<RetryReason>? _retryReasons;
        private IRetryStrategy? _retryStrategy;
        private volatile bool _isSent;
        private readonly Stopwatch _stopwatch;
        private IRequestSpan? _dispatchSpan;
        private bool _isOrphaned;
        private volatile string? _lastDispatchedFrom;
        private volatile string? _lastDispatchedTo;
        private volatile bool _resilient;
        protected OperationBase()
        {
            Opaque = SequenceGenerator.GetNext();
            Header = new OperationHeader { Status = ResponseStatus.None };
            Key = string.Empty;
            _stopwatch = Stopwatch.StartNew();
        }

        #region IOperation Properties

        public virtual bool PreserveTtl { get; set; } = false;

        /// <inheritdoc />
        public abstract OpCode OpCode { get; }

        /// <inheritdoc />
        public string? BucketName { get; set; }

        /// <inheritdoc />
        public string? SName { get; set; }

        /// <inheritdoc />
        public string? CName { get; set; }

        /// <inheritdoc />
        public uint? Cid { get; set; }

        /// <inheritdoc />
        public string Key { get; set; }

        /// <inheritdoc />
        public uint Opaque { get; set; }

        /// <inheritdoc />
        public ulong Cas { get; set; }

        /// <inheritdoc />
        public short? VBucketId { get; set; }

        /// <inheritdoc />
        public short? ReplicaIdx { get; protected set; }

        /// <inheritdoc />
        public OperationHeader Header { get; set; }

        /// <inheritdoc />
        public IRequestSpan Span
        {
            get => _span ?? NoopRequestSpan.Instance;
            internal set
            {
                _span = value;
                _span.WithOperationId(this);
            }
        }

        /// <inheritdoc />
        [Obsolete("Unused, will be removed in a future version.")]
        public IValueRecorder Recorder
        {
            get => NoopValueRecorder.Instance;
            set { }
        }

        /// <inheritdoc />
        public virtual bool HasDurability => false;

        /// <inheritdoc />
        public virtual bool IsReadOnly => true;

        /// <inheritdoc />
        public bool IsSent
        {
            get => _isSent;
            internal set => _isSent = value; // For unit tests
        }

        /// <inheritdoc />
        public ValueTask<ResponseStatus> Completed => new(this, _valueTaskSource.Version);

        /// <inheritdoc />
        public virtual bool RequiresVBucketId => true;

        #endregion

        #region IRequest Properties (RetryAsync SDK-3)

        public uint Attempts { get; set; }
        bool IRequest.Idempotent => IsReadOnly;

        public List<RetryReason> RetryReasons
        {
            get => _retryReasons ??= new List<RetryReason>();
            set => _retryReasons = value;
        }

        public IRetryStrategy RetryStrategy
        {
            get => _retryStrategy ??= new BestEffortRetryStrategy(new ControlledBackoff());
            set => _retryStrategy = value;
        }

        public TimeSpan Timeout { get; set; }

        public CancellationTokenPair TokenPair { get; set; }

        public CancellationToken Token
        {
            get => TokenPair;
            set => throw new NotImplementedException();
        }

        // Not necessary for operations, just throw NotImplementedException and avoid an unnecessary backing field
        public string? ClientContextId
        {
            get => null;
            set => throw new NotImplementedException();
        }

        // Not necessary for operations, just throw NotImplementedException and avoid an unnecessary backing field
        public string? Statement
        {
            get => null;
            set => throw new NotImplementedException();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Expiration to apply for mutation operations, and returns the expiration after the operation is completed.
        /// </summary>
        public uint Expires { get; set; }

        /// <summary>
        /// Flags returned in the operation response.
        /// </summary>
        public Flags Flags { get; protected set; }

        /// <summary>
        /// Mutation token returned by mutation operations, if any.
        /// </summary>
        public MutationToken? MutationToken { get; private set; }

        /// <summary>
        /// Transcoder used for reading and writing the body of the operation.
        /// </summary>
        public ITypeTranscoder Transcoder { get; set; } = null!; // Assumes we always initialize with OperationConfigurator

        /// <summary>
        /// Service for compressing and decompressing operation bodies. Typically set by the <see cref="IOperationConfigurator"/>.
        /// </summary>
        public IOperationCompressor OperationCompressor { get; set; } = null!; // Assumes we always initialize with OperationConfigurator

        /// <summary>
        /// Service which providers <see cref="OperationBuilder"/> instances as needed.
        /// </summary>
        public ObjectPool<OperationBuilder> OperationBuilderPool { get; set; } = null!;  // Assumes we always initialize with OperationConfigurator

        public TimeSpan Elapsed => _stopwatch.Elapsed;

        #endregion

        #region Protected Properties

        /// <summary>
        /// Exception encountered when parsing data, if any.
        /// </summary>
        protected Exception? Exception { get; set; }

        /// <summary>
        /// Response data.
        /// </summary>
        protected ReadOnlyMemory<byte> Data => _data.Memory;

        /// <summary>
        /// Overriden in derived operation classes that support request body compression. If true is returned,
        /// and if compression has been negotiated with the server, the body will be compressed after the call
        /// to <see cref="WriteBody"/>.
        /// </summary>
        protected virtual bool SupportsRequestCompression => false;

        #endregion

        #region Async Completion

        /// <summary>
        /// Allows us to add TryXXX completions on top of ManualResetValueTaskSourceCore by using Interlocked.Exchange
        /// on this value. 1 = completed (in any form), 0 = not completed.
        /// </summary>
        private volatile int _isCompleted = 0;

        private ManualResetValueTaskSourceCore<ResponseStatus> _valueTaskSource;

        /// <inheritdoc />
        public ResponseStatus GetResult(short token) => _valueTaskSource.GetResult(token);

        /// <inheritdoc />
        public ValueTaskSourceStatus GetStatus(short token) => _valueTaskSource.GetStatus(token);

        /// <inheritdoc />
        public void OnCompleted(Action<object?> continuation, object? state, short token,
            ValueTaskSourceOnCompletedFlags flags) =>
            _valueTaskSource.OnCompleted(continuation, state, token, flags);

        /// <inheritdoc />
        public virtual void Reset()
        {
            Reset(ResponseStatus.None);
        }

        private void Reset(ResponseStatus status)
        {
            _data.Dispose();
            _data = SlicedMemoryOwner<byte>.Empty;
            Opaque = SequenceGenerator.GetNext();

            Header = new OperationHeader
            {
                Magic = Header.Magic,
                OpCode = OpCode,
                Cas = Header.Cas,
                BodyLength = Header.BodyLength,
                Key = Key,
                Status = status,
                Opaque = Opaque
            };

            _isSent = false;

            _valueTaskSource.Reset();
            _isCompleted = 0;
        }

        #endregion

        #region Read

        public void Read(in SlicedMemoryOwner<byte> buffer)
        {
            EnsureNotDisposed();

            Header = buffer.Memory.Span.CreateHeader();
            Cas = Header.Cas;
            _data = buffer;

            ReadExtras(_data.Memory.Span);
        }

        protected virtual void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > Header.ExtrasOffset)
            {
                Flags = Flags.Read(buffer.Slice(Header.ExtrasOffset));

                Expires = ByteConverter.ToUInt32(buffer.Slice(25));

                TryReadServerDuration(buffer);
            }
        }

        protected void TryReadServerDuration(ReadOnlySpan<byte> buffer)
        {
            //Fetch the time taken on the server
            LastServerDuration = Header.GetServerDuration(buffer);
        }

        protected void TryReadMutationToken(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length >= 40 && VBucketId.HasValue)
            {
                var uuid = ByteConverter.ToInt64(buffer.Slice(Header.ExtrasOffset));
                var seqno = ByteConverter.ToInt64(buffer.Slice(Header.ExtrasOffset + 8));
                MutationToken = new MutationToken(BucketName, VBucketId.Value, uuid, seqno);
            }
        }

        /// <inheritdoc />
        public BucketConfig? ReadConfig(ITypeTranscoder transcoder)
        {
            BucketConfig? config = null;
            if (GetResponseStatus() == ResponseStatus.VBucketBelongsToAnotherServer && Data.Length > 0)
            {
                var offset = Header.BodyOffset;
                var length = Header.TotalLength - Header.BodyOffset;

                //Override any flags settings since the body of the response has changed to a config
                config = transcoder.Decode<BucketConfig>(Data.Slice(offset, length), new Flags
                {
                    Compression = Compression.None,
                    DataFormat = DataFormat.Json,
                    TypeCode = TypeCode.Object
                }, OpCode);
            }
            return config;
        }

        /// <summary>
        /// Set an attribute on the internal Span indicating that he operation
        /// has been orphaned and has no response if the op is in-flight and the
        /// Span is currently writable.
        /// </summary>
        public void LogOrphaned()
        {
            if (Span.CanWrite && IsSent && !_isOrphaned)
            {
                _isOrphaned = true;//only create attribute once
                Span.SetAttribute("orphaned", "true");
                MetricTracker.KeyValue.TrackOrphaned();
            }
        }

        /// <inheritdoc />
        public SlicedMemoryOwner<byte> ExtractBody()
        {
            if (Header.BodyOffset >= _data.Memory.Length)
            {
                // Empty body, just free the memory
                _data.Dispose();
                _data = SlicedMemoryOwner<byte>.Empty;

                return SlicedMemoryOwner<byte>.Empty;
            }

            if ((Header.DataType & DataType.Snappy) != DataType.None)
            {
                var result = OperationCompressor.Decompress(_data.Memory.Slice(Header.BodyOffset), Span);

                // We can free the compressed memory now. Don't do this until after decompression in case an exception is thrown.
                _data.Dispose();
                _data = SlicedMemoryOwner<byte>.Empty;

                return new SlicedMemoryOwner<byte>(result);
            }
            else
            {
                var data = _data.Slice(Header.BodyOffset);
                _data = SlicedMemoryOwner<byte>.Empty;
                return data;
            }
        }

        #endregion

        #region Response Handling

        protected virtual void HandleClientError(string message, ResponseStatus responseStatus)
        {
            Reset(responseStatus);
            var msgBytes = Encoding.UTF8.GetBytes(message);

            _data = MemoryPool<byte>.Shared.RentAndSlice(msgBytes.Length);
            msgBytes.AsSpan().CopyTo(_data.Memory.Span);
        }

        public bool GetSuccess()
        {
            return (Header.Status == ResponseStatus.Success || Header.Status == ResponseStatus.AuthenticationContinue) && Exception == null;
        }

        public ResponseStatus GetResponseStatus()
        {
            var status = Header.Status;
            if (Exception != null && status == ResponseStatus.Success)
            {
                //In this case the response was received but parsing the response failed for some reason.
                status = ResponseStatus.ClientFailure;
            }

            //For CB 5.X "LOCKED" is now returned when a key is locked with GetL (surprise, surprise)
            //However, the 2.X SDKs cannot return locked becuase it will not be backwards compatible,
            //so will break the bug that was fixed on the server and set the status back to TEMP_FAIL.
            //This will enable applications that rely on TEMP_FAIL and the special exception to work
            //as they did with pre-5.X servers.
            if (status == ResponseStatus.Locked)
            {
                switch (OpCode)
                {
                    case OpCode.Set:
                    case OpCode.Replace:
                    case OpCode.Delete:
                        status = ResponseStatus.KeyExists;
                        break;
                    default:
                        status = ResponseStatus.TemporaryFailure;
                        break;
                }
            }

            return status;
        }

        #endregion

        #region Writing

        private OperationRequestHeader CreateHeader(DataType dataType) =>
            new()
            {
                OpCode = OpCode,
                VBucketId = VBucketId,
                Opaque = Opaque,
                Cas = Cas,
                DataType = dataType
            };

        /// <summary>
        /// Writes the key to an <see cref="OperationBuilder"/>.
        /// </summary>
        /// <param name="builder">The builder.</param>
        [SkipLocalsInit]
        protected virtual void WriteKey(OperationBuilder builder)
        {
            Span<byte> buffer = stackalloc byte[OperationHeader.MaxKeyLength + Leb128.MaxLength];

            var length = WriteKey(buffer);

            builder.Write(buffer.Slice(0, length));
        }

        /// <summary>
        /// Write the <see cref="Key"/>, with <see cref="Cid"/> if present, to a buffer.
        /// </summary>
        /// <param name="buffer">Target buffer.</param>
        /// <returns>Number of bytes written.</returns>
        protected int WriteKey(Span<byte> buffer)
        {
            var length = 0;

            //Default collection does not need the CID
            if (Cid.HasValue)
            {
                length += Leb128.Write(buffer, Cid.GetValueOrDefault());
            }

            length += ByteConverter.FromString(Key, buffer.Slice(length));

            return length;
        }

        /// <summary>
        /// Writes the extras to an <see cref="OperationBuilder"/>.
        /// </summary>
        /// <param name="builder">The builder.</param>
        protected virtual void WriteExtras(OperationBuilder builder)
        {
        }

        /// <summary>
        /// Writes the framing extras to an <see cref="OperationBuilder"/>.
        /// </summary>
        /// <param name="builder">The builder.</param>
        protected virtual void WriteFramingExtras(OperationBuilder builder)
        {
        }

        /// <summary>
        /// Writes the body to an <see cref="OperationBuilder"/>.
        /// </summary>
        /// <param name="builder">The builder.</param>
        protected virtual void WriteBody(OperationBuilder builder)
        {
        }

        #endregion

        #region Send

        /// <summary>
        /// Prepares the operation to be sent.
        /// </summary>
        protected virtual void BeginSend()
        {
        }

        /// <inheritdoc />
        public virtual async Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            connection.AddTags(Span);

            //useful for debugging
            _lastDispatchedFrom = connection.LocalHost;
            _lastDispatchedTo = connection.RemoteHost;

            using var encodingSpan = Span.EncodingSpan();
            BeginSend();

            var builder = OperationBuilderPool.Get();
            try
            {
                WriteFramingExtras(builder);

                builder.AdvanceToSegment(OperationSegment.Extras);
                WriteExtras(builder);

                builder.AdvanceToSegment(OperationSegment.Key);
                WriteKey(builder);

                builder.AdvanceToSegment(OperationSegment.Body);
                WriteBody(builder);

                var dataType = DataType.None;
                if (SupportsRequestCompression && connection.ServerFeatures.SnappyCompression)
                {
                    if (builder.AttemptBodyCompression(OperationCompressor, encodingSpan))
                    {
                        dataType |= DataType.Snappy;
                    }
                }

                builder.WriteHeader(CreateHeader(dataType));

                var buffer = builder.GetBuffer();

                encodingSpan.Dispose();
                _dispatchSpan = Span.DispatchSpan(this);

                await connection.SendAsync(buffer, this, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                _isSent = true;
            }
            finally
            {
                OperationBuilderPool.Return(builder);
            }
        }

        #endregion

        #region Receive

        /// <inheritdoc />
        public bool TrySetCanceled(CancellationToken cancellationToken = default) =>
            TrySetException(new OperationCanceledException(cancellationToken));

        /// <inheritdoc />
        public bool TrySetException(Exception ex) =>
            TrySetException(ex, false);

        private bool TrySetException(Exception ex, bool ignoreIsCompleted)
        {
            if (!ignoreIsCompleted)
            {
                var prevCompleted = Interlocked.Exchange(ref _isCompleted, 1);
                if (prevCompleted == 1)
                {
                    return false;
                }
            }

            _valueTaskSource.SetException(ex);
            return true;
        }

        /// <inheritdoc />
        public void HandleOperationCompleted(in SlicedMemoryOwner<byte> data)
        {
            _dispatchSpan?.Dispose();
            var prevCompleted = Interlocked.Exchange(ref _isCompleted, 1);
            if (prevCompleted == 1)
            {
                StopRecording();
                data.Dispose();
                return;
            }

            var status = (ResponseStatus) ByteConverter.ToInt16(data.Memory.Span.Slice(HeaderOffsets.Status));

            try
            {
                if (status == ResponseStatus.Success
                    || status == ResponseStatus.VBucketBelongsToAnotherServer
                    || status == ResponseStatus.AuthenticationContinue
                    || status == ResponseStatus.SubDocMultiPathFailure
                    || status == ResponseStatus.SubdocMultiPathFailureDeleted
                    || status == ResponseStatus.SubDocSuccessDeletedDocument)
                {
                    Read(in data);
                }
                else
                {
                    data.Dispose();
                }

                //Fetch the time taken on the server
                if (Span.CanWrite)
                {
                    if (LastServerDuration.HasValue)
                    {
                        Span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.ServerDuration,
                            LastServerDuration.Value.ToStringInvariant());
                    }

                    //Write the timeout_ms span attribute
                    Span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.TimeoutMilliseconds,
                        Timeout.TotalMilliseconds.ToStringInvariant()!);
                }

                _valueTaskSource.SetResult(status);
            }
            catch (Exception ex)
            {
                TrySetException(ex, true);
                data.Dispose();
            }
            finally
            {
                //for measuring latency using an LoggingMeter or similar.
                StopRecording();
            }
        }

        /// <inheritdoc />
        public bool WasNmvb()
        {
            return Header.Status == ResponseStatus.VBucketBelongsToAnotherServer;
        }

        public long? LastServerDuration { get; protected set; }

        public string? LastDispatchedFrom => _lastDispatchedFrom;

        public string? LastDispatchedTo => _lastDispatchedTo;

        public bool Resilient { get => _resilient; set => _resilient = value; }

        #endregion

        #region Tracing and Metrics

        /// <inheritdoc />
        public void StopRecording()
        {
            _stopwatch.Stop();
            MetricTracker.KeyValue.TrackOperation(OpCode, _stopwatch.Elapsed);
        }
        #endregion

        #region Finalization and Dispose

        private bool _disposed;

        /// <inheritdoc />
        /// <remarks>
        /// It is important for this object to be disposed. Failure to properly dispose this object may result
        /// in memory not being returned to the ArrayPool, which will increase GC impact across various parts of the framework.
        /// </remarks>
        public void Dispose()
        {
            _disposed = true;
            _data.Dispose();
            _data = SlicedMemoryOwner<byte>.Empty;
        }

        protected void EnsureNotDisposed()
        {
            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposedException(GetType().FullName!);
            }
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
