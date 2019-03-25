using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations.Legacy.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Utils;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Operations.Legacy
{
    internal abstract class OperationBase : IOperation
    {
        internal Flags Flags;
        public const int DefaultRetries = 2;
        protected static MutationToken DefaultMutationToken = new MutationToken(null, -1, -1, -1);
        internal ErrorCode ErrorCode;
        private static readonly IByteConverter DefaultConverter = new DefaultConverter();
        private static readonly ITypeTranscoder DefaultTranscoder = new DefaultTranscoder(DefaultConverter);

        protected OperationBase()
        {
            Opaque = SequenceGenerator.GetNext();
            Data = MemoryStreamFactory.GetMemoryStream();
            Header = new OperationHeader {Status = ResponseStatus.None};
            Key = string.Empty;

            //temporarily make a static - later should be pluggable/set externally
            Converter = DefaultConverter;
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
        public MemoryStream Data { get; set; }
        public uint Opaque { get; set; }
        public short? VBucketId { get; set; }
        public int TotalLength => Header.TotalLength;
        public virtual bool Success => GetSuccess();
        public uint Expires { get; set; }
        public int Attempts { get; set; }
        public int MaxRetries { get; set; }
        public DateTime CreationTime { get; set; }
        public Func<SocketAsyncState, Task> Completed { get; set; }

        public virtual void Reset()
        {
            Reset(ResponseStatus.Success);
        }

        public virtual void Reset(ResponseStatus status)
        {
            Data?.Dispose();
            Data = MemoryStreamFactory.GetMemoryStream();

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

        public virtual void HandleClientError(string message, ResponseStatus responseStatus)
        {
            Reset(responseStatus);
            var msgBytes = Encoding.UTF8.GetBytes(message);
            if (Data == null)
            {
                Data = MemoryStreamFactory.GetMemoryStream();
            }
            Data.Write(msgBytes, 0, msgBytes.Length);
        }

        public Task ReadAsync(byte[] buffer, ErrorMap errorMap = null)
        {
            var header = buffer.AsSpan().CreateHeader(errorMap, out var errorCode);
            return ReadAsync(buffer, header, errorCode);
        }

        public async Task ReadAsync(byte[] buffer, OperationHeader header, ErrorCode errorCode = null)
        {
            Header = header;
            ErrorCode = errorCode;

            if (buffer?.Length > 0)
            {
                await Data.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }
        }

        public OperationHeader ReadHeader()
        {
            return new OperationHeader();
        }

        public virtual Task<byte[]> WriteAsync()
        {
            var tcs = new TaskCompletionSource<byte[]>();
            try
            {
                tcs.SetResult(Write());
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
            return tcs.Task;
        }

        public virtual byte[] CreateHeader(byte[] extras, byte[] body, byte[] key, byte[] framingExtras)
        {
            var header = new byte[OperationHeader.Length];
            var totalLength = extras.GetLengthSafe() + key.GetLengthSafe() + body.GetLengthSafe() + framingExtras.GetLengthSafe();

            if (framingExtras.GetLengthSafe() > 0)
            {
                Converter.FromByte((byte) Magic.AltRequest, header, HeaderOffsets.Magic);
                Converter.FromByte((byte) framingExtras.GetLengthSafe(), header, HeaderOffsets.KeyLength);
                Converter.FromByte((byte) key.GetLengthSafe(), header, HeaderOffsets.AltKeyLength);
            }
            else
            {
                Converter.FromByte((byte) Magic.Request, header, HeaderOffsets.Magic);
                Converter.FromInt16((short) key.GetLengthSafe(), header, HeaderOffsets.KeyLength);
            }

            Converter.FromByte((byte)OpCode, header, HeaderOffsets.Opcode);
            Converter.FromByte((byte)extras.GetLengthSafe(), header, HeaderOffsets.ExtrasLength);

            if (VBucketId.HasValue)
            {
                Converter.FromInt16(VBucketId.Value, header, HeaderOffsets.VBucket);
            }

            Converter.FromInt32(totalLength, header, HeaderOffsets.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderOffsets.Opaque);
            Converter.FromUInt64(Cas, header, HeaderOffsets.Cas);

            return header;
        }

        public virtual void ReadExtras(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > Header.ExtrasOffset)
            {
                var format = new byte();
                var flags = Converter.ToByte(buffer.Slice(Header.ExtrasOffset));
                Converter.SetBit(ref format, 0, Converter.GetBit(flags, 0));
                Converter.SetBit(ref format, 1, Converter.GetBit(flags, 1));
                Converter.SetBit(ref format, 2, Converter.GetBit(flags, 2));
                Converter.SetBit(ref format, 3, Converter.GetBit(flags, 3));

                var compression = new byte();
                Converter.SetBit(ref compression, 4, Converter.GetBit(flags, 4));
                Converter.SetBit(ref compression, 5, Converter.GetBit(flags, 5));
                Converter.SetBit(ref compression, 6, Converter.GetBit(flags, 6));

                var typeCode = (TypeCode)(Converter.ToUInt16(buffer.Slice(26)) & 0xff);
                Format = (DataFormat)format;
                Compression = (Compression) compression;
                Flags.DataFormat = Format;
                Flags.Compression = Compression;
                Flags.TypeCode = typeCode;
                Expires = Converter.ToUInt32(buffer.Slice(25));
            }
        }

        public virtual byte[] CreateKey()
        {
            var length = Encoding.UTF8.GetByteCount(Key);

            //for collections add the leb128 cid
            if (Cid.HasValue)
            {
                length = length + 2;
            }
            var buffer = new byte[length];
            if (Cid.HasValue)
            {
                var leb128Length = Leb128.Write(buffer, Cid.Value);
                Converter.FromString(Key, buffer, leb128Length);
            }
            else
            {
                Converter.FromString(Key, buffer, 0);
            }
            return buffer;
        }

        public IOperationResult GetResult()
        {
            var result = new OperationResult {Id = Key};
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
                if (Success && Data != null && Data.Length > 0)
                {
                    var buffer = Data.ToArray();
                    ReadExtras(buffer);
                    result.Token = MutationToken ?? DefaultMutationToken;
                }

                //clean up and set to null
                if (!result.IsNmv())
                {
                    Data?.Dispose();
                    Data = null;
                }
            }
            catch (Exception e)
            {
                result.Exception = e;
                result.Success = false;
                result.Status = ResponseStatus.ClientFailure;
            }
            finally
            {
                if (Data != null && !result.IsNmv())
                {
                    Data.Dispose();
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
                message = string.Format("Status code: {0} [{1}]", status, (int) status);
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
                    var context = (string) response.error.context;
                    var reference = (string) response.error.@ref;

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
            if (GetResponseStatus() != ResponseStatus.Success && Data != null && Data.Length > 0)
            {
                var buffer = Data.ToArray();
                if (buffer.Length > 0 && TotalLength == OperationHeader.Length)
                {
                    body = Converter.ToString(buffer, 0, buffer.Length);
                }
                else
                {
                    body = Converter.ToString(buffer, OperationHeader.Length, Math.Min(buffer.Length - OperationHeader.Length, TotalLength - OperationHeader.Length));
                }
            }

            return body;
        }

        public virtual BucketConfig GetConfig(ITypeTranscoder transcoder)
        {
            BucketConfig config = null;
            if (GetResponseStatus() == ResponseStatus.VBucketBelongsToAnotherServer && Data != null)
            {
                var offset = Header.BodyOffset;
                var length = Header.TotalLength - Header.BodyOffset;

                //Override any flags settings since the body of the response has changed to a config
                config = transcoder.Decode<BucketConfig>(Data.ToArray(), offset, length, new Flags
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
            return ErrorCode?.Retry != null && ErrorCode.Retry.Strategy != RetryStrategy.None;
        }

        public ITypeTranscoder Transcoder { get; set; }

        public MutationToken MutationToken { get; protected set; }

        public virtual byte[] CreateExtras()
        {
            return Array.Empty<byte>();
        }

        public virtual byte[] CreateBody()
        {
            return Array.Empty<byte>();
        }

        public virtual byte[] CreateFramingExtras()
        {
            return Array.Empty<byte>();
        }

        public virtual byte[] Write()
        {
            var extras = CreateExtras();
            var key = CreateKey();
            var body = CreateBody();
            var framingExtras = CreateFramingExtras();
            var header = CreateHeader(extras, body, key, framingExtras);

            var buffer = new byte[extras.GetLengthSafe() +
                                  body.GetLengthSafe() +
                                  key.GetLengthSafe() +
                                  header.GetLengthSafe() +
                                  framingExtras.GetLengthSafe()];

            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(framingExtras, 0, buffer, header.Length, framingExtras.Length);
            Buffer.BlockCopy(extras, 0, buffer, header.Length + framingExtras.Length, extras.Length);
            Buffer.BlockCopy(key, 0, buffer, header.Length + framingExtras.Length + extras.Length, key.Length);
            Buffer.BlockCopy(body, 0, buffer, header.Length + framingExtras.Length + extras.Length + key.Length, body.Length);

            return buffer;
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

        public IByteConverter Converter { get; set; }

        protected void TryReadMutationToken(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length >= 40 && VBucketId.HasValue)
            {
                var uuid = Converter.ToInt64(buffer.Slice(Header.ExtrasOffset));
                var seqno = Converter.ToInt64(buffer.Slice(Header.ExtrasOffset + 8));
                MutationToken = new MutationToken(BucketName, VBucketId.Value, uuid, seqno);
            }
        }

        private short _keyLength;

        public virtual short KeyLength
        {
            get
            {
                if (_keyLength == 0)
                {
                    _keyLength = (short)Encoding.UTF8.GetByteCount(Key);
                }
                return _keyLength;
            }
        }

        public virtual short ExtrasLength { get; protected set; }

        protected int _bodyLength = 0;
        public virtual int BodyLength
        {
            get
            {
                if (_bodyLength == 0)
                {
                    BodyBytes = CreateBody();
                    _bodyLength = BodyBytes.Length;
                }
                return _bodyLength;
            }
        }

        public virtual byte[] BodyBytes { get; protected set; }

        public virtual short PathLength { get; protected set; }

        public virtual bool RequiresKey => true;

        public string BucketName { get; set; }

        public virtual void WriteHeader(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        #region Temp add for sub doc - should refactor

        public virtual void WriteBody(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public virtual void WriteExtras(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public virtual void WriteKey(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public virtual void WritePath(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
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
