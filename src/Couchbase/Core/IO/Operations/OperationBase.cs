using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Core.Utils;
using Couchbase.Diagnostics;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Operations
{
    internal abstract class OperationBase : IOperation
    {
        internal Flags Flags;
        public const int DefaultRetries = 2;
        protected static MutationToken DefaultMutationToken = new MutationToken(null, -1, -1, -1);
        internal ErrorCode ErrorCode;
        private static readonly ITypeTranscoder DefaultTranscoder = new LegacyTranscoder();
        private IMemoryOwner<byte> _data;
        private IInternalSpan _span;

        private TaskCompletionSource<ResponseStatus> _completed = new TaskCompletionSource<ResponseStatus>();

        protected OperationBase()
        {
            Opaque = SequenceGenerator.GetNext();
            Header = new OperationHeader { Status = ResponseStatus.None };
            Key = string.Empty;

            //temporarily make a static - later should be pluggable/set externally
            Transcoder = DefaultTranscoder;
        }

        public abstract OpCode OpCode { get; }
        public OperationHeader Header { get; set; }
        public DataFormat Format { get; set; }
        public Compression Compression { get; set; }
        public string Key { get; set; }
        public Exception Exception { get; set; }
        public ulong Cas { get; set; }
        public uint? Cid { get; set; }
        public Memory<byte> Data => _data?.Memory ?? Memory<byte>.Empty;
        public uint Opaque { get; set; }
        public short? VBucketId { get; set; }
        public short ReplicaIdx { get; set; }
        public int TotalLength => Header.TotalLength;
        public virtual bool Success => GetSuccess();
        public uint Expires { get; set; }
        public string CName { get; set; }
        public string SName { get; set; }

        public IInternalSpan Span
        {
            get => _span ?? NullRequestTracer.NullSpanInstance;
            internal set
            {
                _span = value;
                _span.OperationId(this);
            }
        }

        #region RetryAsync SDK-3

        public uint Attempts { get; set; }
        public virtual bool Idempotent { get; } = false;
        public Dictionary<RetryReason, Exception> Exceptions { get; set; }
        public List<RetryReason> RetryReasons { get; set; } = new List<RetryReason>();
        public IRetryStrategy RetryStrategy { get; set; } = new BestEffortRetryStrategy(new ControlledBackoff());
        public TimeSpan Timeout { get; set; }
        public CancellationToken Token { get; set; }
        public string ClientContextId { get; set; }
        public string Statement { get; set; }

        #endregion

        public DateTime CreationTime { get; set; }

        public Task<ResponseStatus> Completed => _completed.Task;

        public virtual void Reset()
        {
            Reset(ResponseStatus.None);
        }

        public virtual void Reset(ResponseStatus status)
        {
            _data?.Dispose();
            _data = null;
            _completed = new TaskCompletionSource<ResponseStatus>();

            Header = new OperationHeader
            {
                Magic = Header.Magic,
                OpCode = OpCode,
                Cas = Header.Cas,
                BodyLength = Header.BodyLength,
                Key = Key,
                Status = status
            };
        }

        public IMemoryOwner<byte> ExtractData()
        {
            var data = _data;
            _data = null;
            return data;
        }

        public virtual bool HasDurability => false;

        public virtual void HandleClientError(string message, ResponseStatus responseStatus)
        {
            Reset(responseStatus);
            var msgBytes = Encoding.UTF8.GetBytes(message);

            _data = MemoryPool<byte>.Shared.RentAndSlice(msgBytes.Length);
            msgBytes.AsSpan().CopyTo(_data.Memory.Span);
        }

        public void Read(IMemoryOwner<byte> buffer, ErrorMap errorMap = null)
        {
            EnsureNotDisposed();

            var header = buffer.Memory.Span.CreateHeader(errorMap, out var errorCode);
            Read(buffer, header, errorCode);
        }

        private void Read(IMemoryOwner<byte> buffer, OperationHeader header, ErrorCode errorCode = null)
        {
            Header = header;
            ErrorCode = errorCode;
            Cas = header.Cas;
            _data = buffer;

            ReadExtras(_data.Memory.Span);
        }

        public OperationHeader ReadHeader()
        {
            return new OperationHeader();
        }

        protected virtual OperationRequestHeader CreateHeader()
        {
            return new OperationRequestHeader
            {
                OpCode = OpCode,
                VBucketId = VBucketId,
                Opaque = Opaque,
                Cas = Cas
            };
        }

        public virtual void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > Header.ExtrasOffset)
            {
                var format = new byte();
                var flags = buffer[Header.ExtrasOffset];
                BitUtils.SetBit(ref format, 0, BitUtils.GetBit(flags, 0));
                BitUtils.SetBit(ref format, 1, BitUtils.GetBit(flags, 1));
                BitUtils.SetBit(ref format, 2, BitUtils.GetBit(flags, 2));
                BitUtils.SetBit(ref format, 3, BitUtils.GetBit(flags, 3));

                var compression = new byte();
                BitUtils.SetBit(ref compression, 4, BitUtils.GetBit(flags, 4));
                BitUtils.SetBit(ref compression, 5, BitUtils.GetBit(flags, 5));
                BitUtils.SetBit(ref compression, 6, BitUtils.GetBit(flags, 6));

                var typeCode = (TypeCode)(ByteConverter.ToUInt16(buffer.Slice(26)) & 0xff);
                Format = (DataFormat)format;
                Compression = (Compression)compression;
                Flags.DataFormat = Format;
                Flags.Compression = Compression;
                Flags.TypeCode = typeCode;
                Expires = ByteConverter.ToUInt32(buffer.Slice(25));
            }
        }

        public virtual void WriteKey(OperationBuilder builder)
        {
            using (var bufferOwner = MemoryPool<byte>.Shared.Rent(OperationHeader.MaxKeyLength + Leb128.MaxLength))
            {
                var length = WriteKey(bufferOwner.Memory.Span);

                builder.Write(bufferOwner.Memory.Slice(0, length));
            }
        }

        protected int WriteKey(Span<byte> buffer)
        {
            var length = 0;

            if (Cid.HasValue)
            {
                length += Leb128.Write(buffer, Cid.Value);
            }

            length += ByteConverter.FromString(Key, buffer.Slice(length));

            return length;
        }

        public IOperationResult GetResult()
        {
            var result = new OperationResult { Id = Key };
            try
            {
                result.Success = GetSuccess();
                result.Message = GetMessage();
                result.Status = GetResponseStatus();
                result.Cas = Header.Cas;
                result.Exception = Exception;
                result.Token = MutationToken ?? DefaultMutationToken;
                result.Id = Key;
                result.OpCode = OpCode;

                // make sure we read any extras
                if (Data.Length > 0)
                {
                    ReadExtras(_data.Memory.Span);
                    result.Token = MutationToken ?? DefaultMutationToken;
                }

                //clean up and set to null
                if (!result.IsNmv())
                {
                    Dispose();
                }
            }
            catch (Exception e)
            {
                result.Exception = e;
                result.Success = false;
                result.Status = ResponseStatus.Failure;
            }
            finally
            {
                if (_data != null && !result.IsNmv())
                {
                    Dispose();
                }
            }
            return result;
        }

        public virtual bool GetSuccess()
        {
            return (Header.Status == ResponseStatus.Success || Header.Status == ResponseStatus.AuthenticationContinue) && Exception == null;
        }

        public virtual ResponseStatus GetResponseStatus()
        {
            var status = Header.Status;
            if (Exception != null && status == ResponseStatus.Success)
            {
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

        public string GetMessage()
        {
            if (Success)
            {
                return string.Empty;
            }

            if (Header.Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                return ResponseStatus.VBucketBelongsToAnotherServer.ToString();
            }

            // Read the status and response body
            var status = GetResponseStatus();
            var responseBody = GetResponseBodyAsString();

            // If the status is temp failure and response (string or JSON) contains "lock_error", create a temp lock error
            if (status == ResponseStatus.TemporaryFailure &&
                responseBody.IndexOf("lock_error", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Exception = new TemporaryLockFailureException(ExceptionUtil.TemporaryLockErrorMsg.WithParams(Key));
            }

            // Try and figure out the most descriptive message
            string message;
            if (ErrorCode != null)
            {
                message = ErrorCode.ToString();
            }
            else if (Exception != null)
            {
                message = Exception.Message;
            }
            else
            {
                message = string.Format("Status code: {0} [{1}]", status, (int)status);
            }

            // If JSON bit is not set there is no additional information
            if (!Header.DataType.HasFlag(DataType.Json))
            {
                return message;
            }

            try
            {
                // Try and get the additional error context and reference information from response body
                var response = JsonConvert.DeserializeObject<dynamic>(responseBody);
                if (response != null && response.error != null)
                {
                    // Read context and ref data from reponse body
                    var context = (string)response.error.context;
                    var reference = (string)response.error.@ref;

                    // Append context and reference data to message
                    message = FormatMessage(message, context, reference);

                    // Create KV exception if missing and add context and referece data
                    //Exception = Exception ?? new CouchbaseKeyValueResponseException(message, Header.Status);
                    if (Exception != null)
                    {
                        Exception.Data.Add("Context", context);
                        Exception.Data.Add("Ref", reference);
                    }
                }
            }
            catch (JsonReaderException)
            {
                // This means the response body wasn't valid JSON
                // _log.Warn("Expected response body to be JSON but is invalid. {0}", responseBody);
            }

            return message;
        }

        private static string FormatMessage(string message, string context, string reference)
        {
            if (string.IsNullOrEmpty(context) && string.IsNullOrWhiteSpace(reference))
            {
                return message;
            }

            const string defaultValue = "<none>";
            return string.Format("{0} (Context: {1}, Ref #: {2})",
                message,
                string.IsNullOrWhiteSpace(context) ? defaultValue : context,
                string.IsNullOrWhiteSpace(reference) ? defaultValue : reference
            );
        }

        private string GetResponseBodyAsString()
        {
            var body = string.Empty;
            if (GetResponseStatus() != ResponseStatus.Success && Data.Length > 0)
            {
                if (TotalLength == OperationHeader.Length)
                {
                    body = ByteConverter.ToString(Data.Span);
                }
                else
                {
                    body = ByteConverter.ToString(Data.Span.Slice(OperationHeader.Length, Math.Min(Data.Length - OperationHeader.Length, TotalLength - OperationHeader.Length)));
                }
            }

            return body;
        }

        public virtual BucketConfig GetConfig(ITypeTranscoder transcoder)
        {
            BucketConfig config = null;
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

        public virtual bool CanRetry()
        {
            return Cas > 0 || ErrorMapRequestsRetry();
        }

        internal bool ErrorMapRequestsRetry()
        {
            //TODO make work with retry handling
            return false;// return ErrorCode?.RetryAsync != null && ErrorCode.RetryAsync.Strategy != RetryStrategy.None;
        }

        public ITypeTranscoder Transcoder { get; set; }

        public MutationToken MutationToken { get; protected set; }

        public virtual void WriteExtras(OperationBuilder builder)
        {
        }

        public virtual void WriteBody(OperationBuilder builder)
        {
        }

        public virtual void WriteFramingExtras(OperationBuilder builder)
        {
        }

        /// <summary>
        /// Prepares the operation to be sent.
        /// </summary>
        protected virtual void BeginSend()
        {
        }

        public virtual async Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            Span.WithTag(CouchbaseTags.RemoteAddress,
                connection.EndPoint?.ToString() ?? DiagnosticsReportProvider.UnknownEndpointValue);
            Span.WithTag(CouchbaseTags.LocalAddress,
                connection.LocalEndPoint?.ToString() ?? DiagnosticsReportProvider.UnknownEndpointValue);
            Span.WithTag(CouchbaseTags.LocalId, connection.ConnectionId.ToString(CultureInfo.InvariantCulture));

            using var encodingSpan = Span.StartPayloadEncoding();
            BeginSend();

            var builder = OperationBuilderPool.Instance.Rent();
            try
            {
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(HandleOperationCancelled, cancellationToken);
                }

                WriteFramingExtras(builder);

                builder.AdvanceToSegment(OperationSegment.Extras);
                WriteExtras(builder);

                builder.AdvanceToSegment(OperationSegment.Key);
                WriteKey(builder);

                builder.AdvanceToSegment(OperationSegment.Body);
                WriteBody(builder);

                builder.WriteHeader(CreateHeader());

                var buffer = builder.GetBuffer();
                encodingSpan.Dispose();
                using var dispatchSpan = Span.StartDispatch();
                await connection.SendAsync(buffer, HandleOperationCompleted).ConfigureAwait(false);
                dispatchSpan.Dispose();
            }
            finally
            {
                OperationBuilderPool.Instance.Return(builder);
            }
        }

        public void Cancel()
        {
            _completed.TrySetCanceled();
        }

        private void HandleOperationCancelled(object state)
        {
            _completed.TrySetCanceled((CancellationToken)state);
        }

        /// <summary>
        /// Internal for testing purposes only, do not use in SDK.
        /// </summary>
        internal void HandleOperationCompleted(IMemoryOwner<byte> data, ResponseStatus status)
        {
            try
            {
                if (data != null && (status == ResponseStatus.Success
                                     || status == ResponseStatus.VBucketBelongsToAnotherServer
                                     || status == ResponseStatus.AuthenticationContinue
                                     || status == ResponseStatus.SubDocMultiPathFailure
                                     || status ==  ResponseStatus.SubDocSuccessDeletedDocument))
                {
                    Read(data);

                    _completed.TrySetResult(status);
                }
                else
                {
                    data?.Dispose();
                }

                _completed.TrySetResult(status);
            }
            catch (Exception ex)
            {
                _completed.TrySetException(ex);
                data?.Dispose();
            }
        }

        public virtual IOperation Clone()
        {
            throw new NotImplementedException();
        }

        public uint LastConfigRevisionTried { get; set; }

        public IPEndPoint CurrentHost { get; set; }

        public int GetRetryTimeout(int defaultTimeout)
        {
            if (ErrorCode == null)
            {
                return defaultTimeout;
            }

            return ErrorCode.GetNextInterval(Attempts, defaultTimeout);
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

        public virtual bool RequiresKey => true;

        public string BucketName { get; set; }

        #region Finalization and Dispose

        ~OperationBase()
        {
            Dispose(false);
        }

        private bool _disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            _disposed = true;
            _data?.Dispose();
            _data = null;
        }

        protected void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
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
