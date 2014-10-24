using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.ServiceModel;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
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
        private readonly ITypeTranscoder _transcoder;
        private readonly T _value;
        protected readonly IByteConverter Converter;
        protected Flags Flags = new Flags();

        protected OperationBase(IByteConverter converter)
            : this(string.Empty, null, converter)
        {
        }

        protected OperationBase(string key, T value, ITypeTranscoder transcoder, IVBucket vBucket,
            IByteConverter converter)
        {
            Key = key;
            _value = value;
            _transcoder = transcoder;
            _opaque = Interlocked.Increment(ref _sequenceId);
            VBucket = vBucket;
            Converter = converter;
            MaxRetries = DefaultRetries;
        }

        protected OperationBase(string key, T value, IVBucket vBucket, IByteConverter converter)
            : this(key, value, new DefaultTranscoder(converter), vBucket, converter)
        {
        }

        protected OperationBase(string key, IVBucket vBucket, IByteConverter converter)
            : this(key, default(T), new DefaultTranscoder(converter), vBucket, converter)
        {
        }

        protected OperationBase(string key, IVBucket vBucket, IByteConverter converter, ITypeTranscoder transcoder)
            : this(key, default(T), transcoder, vBucket, converter)
        {
        }

        public virtual void Reset()
        {
            Reset(ResponseStatus.Success);
        }

        public virtual void Reset(ResponseStatus status)
        {
            if (Data != null)
            {
                Data.Dispose();
            }
            LengthReceived = 0;
            Data = new MemoryStream();
            Buffer = null;
            Header = new OperationHeader
            {
                Magic = 0,
                OperationCode = OperationCode,
                Cas = 0,
                BodyLength = 0,
                Key = Key,
                Status = status
            };
        }

        public virtual void HandleClientError(string message)
        {
            Reset(ResponseStatus.ClientFailure);
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

        private DataFormat GetFormat()
        {
            var dataFormat = DataFormat.Json;
            var typeCode = Type.GetTypeCode(typeof(T));
            switch (typeCode)
            {
                case TypeCode.Object:
                    if (typeof (T) == typeof (Byte[]))
                    {
                        dataFormat = DataFormat.Binary;
                    }
                    break;
                case TypeCode.Boolean:
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.DateTime:
                    dataFormat = DataFormat.Json;
                    break;
                case TypeCode.Char:
                case TypeCode.String:
                    dataFormat = DataFormat.String;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return dataFormat;
        }

        public virtual byte[] CreateExtras()
        {
            var extras = new byte[8];
            var format = (byte)GetFormat();
            const byte compression = (byte)Compression.None;

            Converter.SetBit(ref extras[0], 0, Converter.GetBit(format, 0));
            Converter.SetBit(ref extras[0], 1, Converter.GetBit(format, 1));
            Converter.SetBit(ref extras[0], 2, Converter.GetBit(format, 2));
            Converter.SetBit(ref extras[0], 3, Converter.GetBit(format, 3));
            Converter.SetBit(ref extras[0], 4, false);
            Converter.SetBit(ref extras[0], 5, Converter.GetBit(compression, 0));
            Converter.SetBit(ref extras[0], 6, Converter.GetBit(compression, 1));
            Converter.SetBit(ref extras[0], 7, Converter.GetBit(compression, 2));

            var typeCode = (ushort)Type.GetTypeCode(typeof(T));
            Converter.FromUInt16(typeCode, extras, 2);
            Converter.FromUInt32(Expires, extras, 4);

            Format = (DataFormat) format;
            Compression = compression;

            Flags.DataFormat = Format;
            Flags.Compression = Compression;
            Flags.TypeCode = (TypeCode)typeCode;

            return extras;
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

        public virtual byte[] CreateBody()
        {
            byte[] bytes;
            if (typeof(T).IsValueType)
            {
                bytes = _transcoder.Encode(RawValue, Flags);
            }
            else
            {
                bytes = RawValue == null ? new byte[0] :
                    _transcoder.Encode(RawValue, Flags);
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
            var value = GetValue();
            return new OperationResult<T>
            {
                Success = GetSuccess(),
                Message = GetMessage(),
                Status = GetResponseStatus(),
                Value = value,
                Cas = Header.Cas,
                Exception = Exception
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
                try
                {
                    var buffer = Data.ToArray();
                    ReadExtras(buffer);
                    result = Transcoder.Decode<T>(buffer, BodyOffset, TotalLength - BodyOffset, Flags);
                }
                catch (Exception e)
                {
                    Exception = e;
                    HandleClientError(e.Message);
                }
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

                //Override any flags settings since the body of the response has changed to a config
                config = Transcoder.Decode<BucketConfig>(Data.ToArray(), offset, length, new Flags
                {
                    Compression = Compression.None,
                    DataFormat = DataFormat.Json,
                    TypeCode = TypeCode.Object
                });
            }
            return config;
        }

        public abstract OperationCode OperationCode { get; }

        public OperationHeader Header { get; set; }

        public OperationBody Body { get; set; }

        public DataFormat Format { get; set; }

        public Compression Compression { get; set; }

        public ITypeTranscoder Transcoder
        {
            get { return _transcoder; }
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