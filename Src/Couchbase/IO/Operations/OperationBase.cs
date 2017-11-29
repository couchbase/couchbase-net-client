﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Utils;
using Couchbase.Logging;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.IO.Operations
{
    internal abstract class OperationBase : IOperation
    {
        private readonly ILog _log = LogManager.GetLogger<OperationBase>();

        private bool _timedOut;
        protected IByteConverter Converter;
        protected Flags Flags;
        private Dictionary<TimingLevel, IOperationTimer> _timers;
        private const int DefaultOffset = 24;
        public const int HeaderLength = 24;
        public const int DefaultRetries = 2;
        protected static MutationToken DefaultMutationToken = new MutationToken(null, -1, -1, -1);
        protected ErrorCode ErrorCode;

        protected OperationBase(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
        {
            if (RequiresKey && string.IsNullOrWhiteSpace(key))
            {
                throw new MissingKeyException();
            }

            Key = key;
            Transcoder = transcoder;
            Opaque = opaque;
            CreationTime = DateTime.UtcNow;
            Timeout = timeout;
            VBucket = vBucket;
            Converter = transcoder.Converter;
            MaxRetries = DefaultRetries;
            Data = MemoryStreamFactory.GetMemoryStream();
            Header = new OperationHeader {Status = ResponseStatus.None};
        }

        protected OperationBase(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : this(key, vBucket, transcoder, SequenceGenerator.GetNext(), timeout)
        {
        }

        public Func<TimingLevel, object, IOperationTimer> Timer { get; set; }
        public abstract OperationCode OperationCode { get; }
        public OperationHeader Header { get; set; }
        public OperationBody Body { get; set; }
        public DataFormat Format { get; set; }
        public Compression Compression { get; set; }
        public string Key { get; protected set; }
        public Exception Exception { get; set; }
        public virtual int BodyOffset { get { return DefaultOffset; } }
        public ulong Cas { get; set; }
        public MemoryStream Data { get; set; }
        public byte[] Buffer { get; set; }
        public uint Opaque { get; protected set; }
        public IVBucket VBucket { get; set; }
        public int LengthReceived { get; protected set; }
        public int TotalLength { get { return Header.TotalLength; }}
        public virtual bool Success { get { return GetSuccess(); } }
        public uint Expires { get; set; }
        public int Attempts { get; set; }
        public int MaxRetries { get; set; }
        public DateTime CreationTime { get; set; }
        public uint Timeout { get; set; }
        public byte[] WriteBuffer { get; set; }
        public Func<SocketAsyncState, Task> Completed { get; set; }

        public virtual void Reset()
        {
            Reset(ResponseStatus.Success);
        }

        public void BeginTimer(TimingLevel level)
        {
            if (Timer != null)
            {
                var timer = Timer(level, this);
                if (_timers == null)
                {
                    _timers = new Dictionary<TimingLevel, IOperationTimer>();
                }
                if (!_timers.ContainsKey(level))
                {
                    _timers.Add(level, timer);
                }
            }
        }

        public void EndTimer(TimingLevel level)
        {
            if (_timers != null && _timers.ContainsKey(level))
            {
                IOperationTimer timer;
                if(_timers.TryGetValue(level, out timer))
                {
                    if (timer != null)
                    {
                        timer.Dispose();
                        _timers.Remove(level);
                    }
                }
            }
        }

        public virtual void Reset(ResponseStatus status)
        {
            if (Data != null)
            {
                Data.Dispose();
            }
            Data = MemoryStreamFactory.GetMemoryStream();
            LengthReceived = 0;

            Header = new OperationHeader
            {
                Magic = Header.Magic,
                OperationCode = OperationCode,
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
            LengthReceived += msgBytes.Length;
            if (Data == null)
            {
                Data = MemoryStreamFactory.GetMemoryStream();
            }
            Data.Write(msgBytes, 0, msgBytes.Length);
        }

        [Obsolete]
        public virtual void Read(byte[] buffer, int offset, int length)
        {
            Read(buffer);
        }

        public void Read(byte[] buffer, ErrorMap errorMap = null)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            ReadContent(buffer, errorMap);
            Data.Write(buffer, 0, buffer.Length);
        }

        [Obsolete]
        public async Task ReadAsync(byte[] buffer, int offset, int length)
        {
            await ReadAsync(buffer).ContinueOnAnyContext();
        }

        public async Task ReadAsync(byte[] buffer, ErrorMap errorMap = null)
        {
            if (buffer == null || buffer.Length == 0)
            {
                return;
            }

            ReadContent(buffer, errorMap);
            await Data.WriteAsync(buffer, 0, buffer.Length).ContinueOnAnyContext();
        }

        private void ReadContent(byte[] buffer, ErrorMap errorMap)
        {
            if (Header.BodyLength == 0 && buffer.Length >= HeaderIndexFor.HeaderLength)
            {
                var status = GetResponseStatus(Converter.ToInt16(buffer, HeaderIndexFor.Status), errorMap);
                Header = new OperationHeader
                {
                    Magic = Converter.ToByte(buffer, HeaderIndexFor.Magic),
                    OperationCode = Converter.ToByte(buffer, HeaderIndexFor.Opcode).ToOpCode(),
                    KeyLength = Converter.ToInt16(buffer, HeaderIndexFor.KeyLength),
                    ExtrasLength = Converter.ToByte(buffer, HeaderIndexFor.ExtrasLength),
                    DataType = (DataType) Converter.ToByte(buffer, HeaderIndexFor.Datatype),
                    Status = status,
                    BodyLength = Converter.ToInt32(buffer, HeaderIndexFor.Body),
                    Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                    Cas = Converter.ToUInt64(buffer, HeaderIndexFor.Cas)
                };

                if (Opaque != Header.Opaque)
                {
                    var msg = string.Format("Expected opaque {0} but got {1}", Opaque, Header.Opaque);
                    HandleClientError(msg, ResponseStatus.ClientFailure);
                }
            }
            LengthReceived += buffer.Length;
        }

        private ResponseStatus GetResponseStatus(short code, ErrorMap errorMap)
        {
            var status = (ResponseStatus) code;

            // Is it a known response status?
            if (Enum.IsDefined(typeof(ResponseStatus), status))
            {
                return status;
            }

            // If available, try and use the error map to get more details
            if (errorMap != null && errorMap.TryGetGetErrorCode(code, out ErrorCode))
            {
                return ResponseStatus.Failure;
            }

            return ResponseStatus.UnknownError;
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

        public virtual byte[] CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new byte[24];
            var totalLength = extras.GetLengthSafe() + key.GetLengthSafe() + body.GetLengthSafe();

            Converter.FromByte((byte)Magic.Request, header, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, header, HeaderIndexFor.Opcode);
            Converter.FromInt16((short)key.GetLengthSafe(), header, HeaderIndexFor.KeyLength);
            Converter.FromByte((byte)extras.GetLengthSafe(), header, HeaderIndexFor.ExtrasLength);

            if (VBucket != null)
            {
                Converter.FromInt16((short)VBucket.Index, header, HeaderIndexFor.VBucket);
            }

            Converter.FromInt32(totalLength, header, HeaderIndexFor.BodyLength);
            Converter.FromUInt32(Opaque, header, HeaderIndexFor.Opaque);
            Converter.FromUInt64(Cas, header, HeaderIndexFor.Cas);

            return header;
        }

        public virtual void ReadExtras(byte[] buffer)
        {
            if (buffer.Length > 24)
            {
                var format = new byte();
                var flags = Converter.ToByte(buffer, 24);
                Converter.SetBit(ref format, 0, Converter.GetBit(flags, 0));
                Converter.SetBit(ref format, 1, Converter.GetBit(flags, 1));
                Converter.SetBit(ref format, 2, Converter.GetBit(flags, 2));
                Converter.SetBit(ref format, 3, Converter.GetBit(flags, 3));

                var compression = new byte();
                Converter.SetBit(ref compression, 4, Converter.GetBit(flags, 4));
                Converter.SetBit(ref compression, 5, Converter.GetBit(flags, 5));
                Converter.SetBit(ref compression, 6, Converter.GetBit(flags, 6));

                var typeCode = (TypeCode)(Converter.ToUInt16(buffer, 26) & 0xff);
                Format = (DataFormat)format;
                Compression = (Compression) compression;
                Flags.DataFormat = Format;
                Flags.Compression = Compression;
                Flags.TypeCode = typeCode;
                Expires = Converter.ToUInt32(buffer, 25);
            }
        }

        public virtual byte[] CreateKey()
        {
            var length = Encoding.UTF8.GetByteCount(Key);
            var buffer = new byte[length];
            Converter.FromString(Key, buffer, 0);
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
                result.OpCode = OperationCode;

                //clean up and set to null
                if (!result.IsNmv())
                {
                    Data.Dispose();
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
                Exception = new TemporaryLockFailureException(ExceptionUtil.TemporaryLockErrorMsg.WithParams(Key));
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
                    Exception = Exception ?? new CouchbaseKeyValueResponseException(message, Header.Status);
                    Exception.Data.Add("Context", context);
                    Exception.Data.Add("Ref", reference);
                }
            }
            catch (JsonReaderException)
            {
                // This means the response body wasn't valid JSON
                _log.Warn("Expected response body to be JSON but is invalid. {0}", responseBody);
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
                if (buffer.Length > 0 && TotalLength == 24)
                {
                    body = Converter.ToString(buffer, 0, buffer.Length);
                }
                else
                {
                    body = Converter.ToString(buffer, 24, Math.Min(buffer.Length - 24, TotalLength - 24));
                }
            }

            return body;
        }

        [Obsolete("Please use Getconfig(ITypeTranscoder) instead.")]
        public virtual IBucketConfig GetConfig()
        {
            return GetConfig(Transcoder);
        }

        public virtual IBucketConfig GetConfig(ITypeTranscoder transcoder)
        {
            IBucketConfig config = null;
            if (GetResponseStatus() == ResponseStatus.VBucketBelongsToAnotherServer && Data != null)
            {
                var offset = HeaderLength + Header.ExtrasLength;
                var length = Header.BodyLength - Header.ExtrasLength;

                //Override any flags settings since the body of the response has changed to a config
                config = transcoder.Decode<BucketConfig>(Data.ToArray(), offset, length, new Flags
                {
                    Compression = Compression.None,
                    DataFormat = DataFormat.Json,
                    TypeCode = TypeCode.Object
                }, OperationCode);
            }
            return config;
        }

        public virtual bool CanRetry()
        {
            return Cas > 0;
        }

        public bool TimedOut()
        {
            if (_timedOut) return _timedOut;

            var elasped = DateTime.UtcNow.Subtract(CreationTime).TotalMilliseconds;
            if (elasped >= Timeout || (ErrorCode != null && ErrorCode.HasTimedOut(elasped)))
            {
                _timedOut = true;
            }
            return _timedOut;
        }

        public ITypeTranscoder Transcoder { get; protected set; }
        public MutationToken MutationToken { get; protected set; }

        public virtual byte[] CreateExtras()
        {
            return new byte[0];
        }

        public virtual byte[] CreateBody()
        {
            return new byte[0];
        }

        public virtual byte[] Write()
        {
            var extras = CreateExtras();
            var key = CreateKey();
            var body = CreateBody();
            var header = CreateHeader(extras, body, key);

            var buffer = new byte[extras.GetLengthSafe() +
                                  body.GetLengthSafe() +
                                  key.GetLengthSafe() +
                                  header.GetLengthSafe()];

            System.Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            System.Buffer.BlockCopy(extras, 0, buffer, header.Length, extras.Length);
            System.Buffer.BlockCopy(key, 0, buffer, header.Length + extras.Length, key.Length);
            System.Buffer.BlockCopy(body, 0, buffer, header.Length + extras.Length + key.Length, body.Length);

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

#region "New" Write API Methods - override and implement these methods for new operations

        public virtual byte[] AllocateBuffer(int length)
        {
            return new byte[length];
        }

        public virtual void WriteHeader(byte[] buffer)
        {
            throw new NotImplementedException();
        }

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

        public virtual bool RequiresKey
        {
            get { return true; }
        }

        public string BucketName { get; set; }
    }

#endregion
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
