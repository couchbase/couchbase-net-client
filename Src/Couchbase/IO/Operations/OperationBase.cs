using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Couchbase.IO.Operations
{
    internal abstract class OperationBase<T> : IOperation<T>
    {
        private const int DefaultOffset = 28;
        public const int HeaderLength = 24;
        private static int _sequenceId;//needs to be resolved - note there will be an instance of the variable for every Type T!
        private readonly int _opaque;
        private readonly ITypeSerializer _serializer;
        private readonly T _value;
        private readonly IVBucket _vBucket;
        protected readonly IByteConverter Converter;

        protected OperationBase(IByteConverter converter)
            : this(string.Empty, null, converter)
        {
        }

        protected OperationBase(string key, T value, ITypeSerializer serializer, IVBucket vBucket, IByteConverter converter)
        {
            Key = key;
            _value = value;
            _serializer = serializer;
            _opaque = Interlocked.Increment(ref _sequenceId);
            _vBucket = vBucket;
            Converter = converter;
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

        public ulong Cas { get; set; }

        public OperationHeader Header { get; set; }

        public OperationBody Body { get; set; }

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
                bytes = RawValue == null ?
                    new byte[0] :
                    _serializer.Serialize(RawValue);
            }

            return bytes;
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

        public virtual IOperationResult<T> GetResult()
        {
            return new OperationResult<T>(this);
        }

        public ITypeSerializer Serializer
        {
            get { return _serializer; }
        }

        public virtual byte[] GetBuffer()
        {
            var extras = CreateExtras();
            var body = CreateBody();
            var key = CreateKey();
            var header = CreateHeader(extras, body, key);
            
            var buffer = new byte[extras.GetLengthSafe() + 
                body.GetLengthSafe() + 
                key.GetLengthSafe() + 
                header.GetLengthSafe()];

            Buffer.BlockCopy(header, 0, buffer, 0, header.Length);
            Buffer.BlockCopy(extras, 0, buffer, header.Length, extras.Length);
            Buffer.BlockCopy(key, 0, buffer, header.Length + extras.Length, key.Length);
            Buffer.BlockCopy(body, 0, buffer, header.Length + extras.Length + key.Length, body.Length);

            return buffer;
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

#endregion [ License information          ]