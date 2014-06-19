using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Couchbase.Utils;

namespace Couchbase.IO.Operations
{
    internal abstract class OperationBase<T> : IOperation<T>
    {
        private const int DefaultOffset = 28;
        public const int HeaderLength = 24;
        private static int _sequenceId;//needs to be resolved
        private readonly int _opaque;
        private readonly ITypeSerializer2 _serializer;
        private readonly T _value;
        private readonly IVBucket _vBucket;
        private readonly IByteConverter _converter;

        protected OperationBase(IByteConverter converter)
            : this(string.Empty, null, converter)
        {
        }

        protected OperationBase(string key, T value, ITypeSerializer2 serializer, IVBucket vBucket, IByteConverter converter)
        {
            Key = key;
            _value = value;
            _serializer = serializer;
            _opaque = Interlocked.Increment(ref _sequenceId);
            _vBucket = vBucket;
            _converter = converter;
        }

        protected OperationBase(string key, T value, IVBucket vBucket, IByteConverter converter)
            : this(key, value, new TypeSerializer2(converter), vBucket, converter)
        {
        }

        protected OperationBase(string key, IVBucket vBucket, IByteConverter converter)
            : this(key, default(T), new TypeSerializer2(converter), vBucket, converter)
        {
        }

        public int Opaque
        {
            get { return _opaque; }
        }

        public T Value
        {
            get
            {
                var offset = HeaderLength + Header.ExtrasLength;
                var length = Header.BodyLength - Header.ExtrasLength;
                return Serializer.Deserialize<T>(Body.Data, offset, length);
            }
        }

        internal T RawValue
        {
            get { return _value; }
        }

        public IVBucket VBucket
        {
            get { return _vBucket; }
        }

        public virtual int Offset
        {
            get { return DefaultOffset; }
        }

        public abstract OperationCode OperationCode { get; }

        public string Key { get; private set; }

        public uint Expires { get; set; }

        public OperationHeader Header { get; set; }

        public OperationBody Body { get; set; }

        public virtual ArraySegment<byte> CreateExtras()
        {
            var extras = new ArraySegment<byte>(new byte[8]);
            var typeCode = Type.GetTypeCode(typeof(T));
            var flag = (uint)((int)typeCode | 0x0100);

            _converter.FromUInt32(flag, extras.Array, 0);
            _converter.FromUInt32(Expires, extras.Array, 4);

            return extras;
        }

        public virtual ArraySegment<byte> CreateKey()
        {
            var bytes = Encoding.UTF8.GetBytes(Key);
            return new ArraySegment<byte>(bytes);
        }

        public virtual ArraySegment<byte> CreateBody()
        {
            byte[] bytes;
            if (typeof (T).IsValueType)
            {
                bytes = _serializer.Serialize(RawValue);
            }
            else
            {
                bytes = RawValue == null ? 
                    new byte[0] : 
                    _serializer.Serialize(RawValue);
            }
            
            return new ArraySegment<byte>(bytes);
        }

        public virtual List<ArraySegment<byte>> CreateBuffer()
        {
            var extras = CreateExtras();
            var body = CreateBody();
            var key = CreateKey();
            var header = CreateHeader(extras.Array, body.Array, key.Array);

            return new List<ArraySegment<byte>>(4)
                {
                    header,
                    extras,
                    key,
                    body
                };
        }

        public virtual ArraySegment<byte> CreateHeader(byte[] extras, byte[] body, byte[] key)
        {
            var header = new ArraySegment<byte>(new byte[24]);
            var totalLength = extras.GetLengthSafe() + key.GetLengthSafe() + body.GetLengthSafe();
            _converter.FromByte((byte)Magic.Request, header.Array, HeaderIndexFor.Magic);
            _converter.FromByte((byte)OperationCode, header.Array, HeaderIndexFor.Opcode);
            _converter.FromInt16((short)key.Length, header.Array, HeaderIndexFor.KeyLength);
            _converter.FromByte((byte)extras.GetLengthSafe(), header.Array, HeaderIndexFor.ExtrasLength);

            if (VBucket != null)
            {
                _converter.FromInt16((short) VBucket.Index, header.Array, HeaderIndexFor.VBucket);
            }

            _converter.FromInt32(totalLength, header.Array, HeaderIndexFor.BodyLength);
            _converter.FromInt32(Opaque, header.Array, HeaderIndexFor.Opaque);
            return header;
        }

        public virtual ArraySegment<byte> CreateHeader2(byte[] extras, byte[] body, byte[] key)
        {
            var header = new ArraySegment<byte>(new byte[24]);
            var buffer = header.Array;
            var totalLength = extras.GetLengthSafe() +
                key.GetLengthSafe() +
                body.GetLengthSafe();

            //0 magic and 1 opcode
            buffer[0x00] = (byte)Magic.Request;
            buffer[0x01] = (byte)OperationCode;

            //2 & 3 Key length
            buffer[0x02] = (byte)(key.Length >> 8);
            buffer[0x03] = (byte)(key.Length & 255);

            //4 extra length
            buffer[0x04] = (byte)extras.GetLengthSafe();

            //5 data type?

            //6 vbucket id
            if (VBucket != null)
            {
                buffer[0x06] = (byte)(VBucket.Index >> 8);
                buffer[0x07] = (byte)(VBucket.Index & 255);
            }

            //8-11 total body length
            buffer[0x08] = (byte)(totalLength >> 24);
            buffer[0x09] = (byte)(totalLength >> 16);
            buffer[0x0a] = (byte)(totalLength >> 8);
            buffer[0x0b] = (byte)(totalLength & 255);

            //12-15 opaque (correlationid)
            buffer[0x0c] = (byte)(Opaque >> 24);
            buffer[0x0d] = (byte)(Opaque >> 16);
            buffer[0x0e] = (byte)(Opaque >> 8);
            buffer[0x0f] = (byte)(Opaque & 255);
            return header;
        }

        public virtual IOperationResult<T> GetResult()
        {
            return new OperationResult<T>(this);
        }

        public ITypeSerializer2 Serializer
        {
            get { return _serializer; }
        }

        //refactor
        public virtual byte[] GetBuffer()
        {
            var buffer = CreateBuffer();
            var bytes = new byte[
                buffer[0].Array.GetLengthSafe() +
                buffer[1].Array.GetLengthSafe() +
                buffer[2].Array.GetLengthSafe() +
                buffer[3].Array.GetLengthSafe()];

            var count = 0;
            foreach (var segment in buffer)
            {
                foreach (var b in segment.ToArray())
                {
                    bytes[count++] = b;
                }
            }
            return bytes;
        }

        public int SequenceId
        {
            get { return _sequenceId + GetHashCode(); }
        }

        public Exception Exception { get; set; }
    }
}

#region [ License information          ]

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

#endregion