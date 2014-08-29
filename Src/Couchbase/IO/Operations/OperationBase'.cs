using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Couchbase.IO.Operations
{
    internal abstract class OperationBase<T> : IOperation<T>
    {
        private const int DefaultOffset = 24;
        public const int HeaderLength = 24;
        public const int DefaultRetries = 2;

        //TODO needs to be resolved - note there will be an instance of the variable for every Type T!
        private static int _sequenceId;

        private readonly int _opaque;
        private readonly ITypeSerializer _serializer;
        private readonly T _value;
        protected readonly IByteConverter Converter;

        protected OperationBase(IByteConverter converter)
            : this(string.Empty, null, converter)
        {
        }

        protected OperationBase(string key, T value, ITypeSerializer serializer, IVBucket vBucket,
            IByteConverter converter)
        {
            Key = key;
            _value = value;
            _serializer = serializer;
            _opaque = Interlocked.Increment(ref _sequenceId);
            VBucket = vBucket;
            Converter = converter;
            MaxRetries = DefaultRetries;
        }

        protected OperationBase(string key, T value, IVBucket vBucket, IByteConverter converter)
            : this(key, value, new TypeSerializer(converter), vBucket, converter)
        {
        }

        protected OperationBase(string key, IVBucket vBucket, IByteConverter converter)
            : this(key, default(T), new TypeSerializer(converter), vBucket, converter)
        {
        }

        protected OperationBase(string key, IVBucket vBucket, IByteConverter converter, ITypeSerializer serializer)
            : this(key, default(T), serializer, vBucket, converter)
        {
        }

        public virtual void Reset()
        {
            if (Data != null)
            {
                Data.Dispose();
            }
            LengthReceived = 0;
            Data = new MemoryStream();
            LengthReceived = 0;
            Buffer = null;
            Header = new OperationHeader();
        }

        public virtual void HandleClientError(string message)
        {
            Header = new OperationHeader
            {
                Magic = 0,
                OperationCode = OperationCode,
                Cas = 0,
                BodyLength = 0,
                Key = Key,
                Status = ResponseStatus.ClientFailure
            };
            var msgBytes = Encoding.UTF8.GetBytes(message);
            LengthReceived += msgBytes.Length;
            if (Data == null)
            {
                Data = new MemoryStream();
            }
            Data.Write(msgBytes, 0, msgBytes.Length);
        }

        public virtual void Read(byte[] buffer, int offset, int length)
        {
            if (Header.BodyLength == 0)
            {
                Header = new OperationHeader
                {
                    Magic = Converter.ToByte(buffer, HeaderIndexFor.Magic),
                    OperationCode = Converter.ToByte(buffer, HeaderIndexFor.Opcode).ToOpCode(),
                    KeyLength = Converter.ToInt16(buffer, HeaderIndexFor.KeyLength),
                    ExtrasLength = Converter.ToByte(buffer, HeaderIndexFor.ExtrasLength),
                    Status = (ResponseStatus)Converter.ToInt16(buffer, HeaderIndexFor.Status),
                    BodyLength = Converter.ToInt32(buffer, HeaderIndexFor.Body),
                    Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                    Cas = Converter.ToUInt64(buffer, HeaderIndexFor.Cas)
                };
            }
            LengthReceived += length;
            Data.Write(buffer, offset, length);
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

        public virtual byte[] CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new byte[24];
            var totalLength = extras.GetLengthSafe() + key.GetLengthSafe() + body.GetLengthSafe();

            Converter.FromByte((byte)Magic.Request, header, HeaderIndexFor.Magic);
            Converter.FromByte((byte)OperationCode, header, HeaderIndexFor.Opcode);
            Converter.FromInt16((short)key.Length, header, HeaderIndexFor.KeyLength);
            Converter.FromByte((byte)extras.GetLengthSafe(), header, HeaderIndexFor.ExtrasLength);

            if (VBucket != null)
            {
                Converter.FromInt16((short)VBucket.Index, header, HeaderIndexFor.VBucket);
            }

            Converter.FromInt32(totalLength, header, HeaderIndexFor.BodyLength);
            Converter.FromInt32(Opaque, header, HeaderIndexFor.Opaque);
            Converter.FromUInt64(Cas, header, HeaderIndexFor.Cas);

            return header;
        }

        public virtual byte[] CreateExtras()
        {
            var extras = new byte[8];
            var typeCode = Type.GetTypeCode(typeof(T));
            var flag = (uint)((int)typeCode | 0x0100);

            Converter.FromUInt32(flag, extras, 0);
            Converter.FromUInt32(Expires, extras, 4);

            return extras;
        }

        public virtual byte[] CreateKey()
        {
            var length = Encoding.UTF8.GetByteCount(Key);
            var buffer = new byte[length];
            Converter.FromString(Key, buffer, 0);
            return buffer;
        }

        public virtual byte[] CreateBody()
        {
            byte[] bytes;
            if (typeof(T).IsValueType)
            {
                bytes = _serializer.Serialize(RawValue);
            }
            else
            {
                bytes = RawValue == null ? new byte[0] :
                    _serializer.Serialize(RawValue);
            }

            return bytes;
        }

        [Obsolete("remove after refactoring async")]
        public byte[] GetBuffer()
        {
            throw new NotImplementedException();
        }

        public virtual Couchbase.IOperationResult<T> GetResult()
        {
            return new OperationResult<T>
            {
                Cas = Header.Cas,
                Message = GetMessage(),
                Status = GetResponseStatus(),
                Success = GetSuccess(),
                Value = GetValue()
            };
        }

        public virtual bool GetSuccess()
        {
            return Header.Status == ResponseStatus.Success && Exception == null;
        }

        public virtual ResponseStatus GetResponseStatus()
        {
            var status = Header.Status;
            if (Exception != null)
            {
                status = ResponseStatus.ClientFailure;
            }
            return status;
        }

        public virtual T GetValue()
        {
            var result = default(T);
            if(Success && Data != null)
            {
                var buffer = Data.ToArray();
                result = Serializer.Deserialize<T>(buffer, BodyOffset, TotalLength - BodyOffset);
            }
            return result;
        }

        public virtual string GetMessage()
        {
            var message = string.Empty;
            if (Success) return message;
            if (Header.Status == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                message = ResponseStatus.VBucketBelongsToAnotherServer.ToString();
            }
            else
            {
                if (Exception == null)
                {
                    try
                    {
                        if (Header.Status != ResponseStatus.Success)
                        {
                            if (Data == null)
                            {
                                message = string.Empty;
                            }
                            else
                            {
                                var buffer = Data.ToArray();
                                if (buffer.Length > 0 && TotalLength == 24)
                                {
                                    message = Converter.ToString(buffer, 0, buffer.Length);
                                }
                                else
                                {
                                    message = Converter.ToString(buffer, 24, TotalLength - 24);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        message = e.Message;
                    }
                }
                else
                {
                    message = Exception.Message;
                }
            }
            return message;
        }

        public virtual IBucketConfig GetConfig()
        {
            IBucketConfig config = null;
            if (GetResponseStatus() == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                var offset = HeaderLength + Header.ExtrasLength;
                var length = Header.BodyLength - Header.ExtrasLength;
                config = Serializer.Deserialize<BucketConfig>(Data.ToArray(), offset, length);
            }
            return config;
        }

        public abstract OperationCode OperationCode { get; }

        public OperationHeader Header { get; set; }

        public OperationBody Body { get; set; }

        public ITypeSerializer Serializer
        {
            get { return _serializer; }
        }

        public int SequenceId
        {
            get { return _sequenceId + GetHashCode(); }
        }

        public string Key { get; protected set; }

        public Exception Exception { get; set; }

        public virtual int BodyOffset
        {
            get { return DefaultOffset; }
        }

        public ulong Cas { get; set; }

        public MemoryStream Data { get; set; }

        public byte[] Buffer { get; set; }

        internal T RawValue
        {
            get { return _value; }
        }

        public int Opaque
        {
            get { return _opaque; }
        }

        public IVBucket VBucket { get; set; }

        public int LengthReceived { get; protected set; }

        public int TotalLength
        {
            get { return Header.TotalLength; }
        }

        public virtual bool Success
        {
            get { return Header.Status == ResponseStatus.Success && Exception == null; }
        }

        public uint Expires { get; set; }

        public int Attempts { get; set; }

        public int MaxRetries { get; set; }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion [ License information ]