using Couchbase.Core;
using Couchbase.Core.Serializers;
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
        public const int HeaderLength = 24;
        private static int _sequenceId;//needs to be resolved
        private readonly int _opaque;
        private readonly ITypeSerializer _serializer;
        private readonly T _value;
        private readonly IVBucket _vBucket;

        protected OperationBase()
            : this(string.Empty, null)
        {
        }

        protected OperationBase(string key, T value, ITypeSerializer serializer, IVBucket vBucket)
        {
            Key = key;
            _value = value;
            _serializer = serializer;
            _opaque = Interlocked.Increment(ref _sequenceId);
            _vBucket = vBucket;
        }

        protected OperationBase(string key, T value, IVBucket vBucket)
            : this(key, value, new TypeSerializer(), vBucket)
        {
        }

        protected OperationBase(string key, IVBucket vBucket)
            : this(key, default(T), new TypeSerializer(), vBucket)
        {
        }

        public int Opaque
        {
            get { return _opaque; }
        }

        public T Value
        {
            get { return Serializer.Deserialize(this); }
        }

        internal T RawValue
        {
            get { return _value; }
        }

        public IVBucket VBucket
        {
            get { return _vBucket; }
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

            BinaryConverter.EncodeUInt32(flag, extras.Array, 0);
            BinaryConverter.EncodeUInt32(Expires, extras.Array, 4);
            return extras;
        }

        public virtual ArraySegment<byte> CreateKey()
        {
            var bytes = Encoding.UTF8.GetBytes(Key);
            return new ArraySegment<byte>(bytes);
        }

        public virtual ArraySegment<byte> CreateBody()
        {
            var bytes = _serializer.Serialize(this);
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

        public ITypeSerializer Serializer
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
