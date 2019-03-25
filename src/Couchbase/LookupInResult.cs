using System;
using System.Collections.Generic;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

namespace Couchbase
{
    public class LookupInResult : ILookupInResult
    {
        private readonly byte[] _bytes;
        private IByteConverter _converter = new DefaultConverter();
        private ITypeSerializer _serializer = new DefaultSerializer();

        internal LookupInResult(byte[] bytes, ulong cas, TimeSpan? expiration)
        {
            _bytes = bytes;
            Cas = cas;
            Expiration = expiration;
        }

        public ulong Cas { get; }
        public TimeSpan? Expiration { get; }

        public T ContentAs<T>(int index)
        {
            var response = _bytes;
            var statusOffset = 24;//Header.BodyOffset;
            var valueLengthOffset = statusOffset + 2;
            var valueOffset = statusOffset + 6;

            var operationSpecs = new List<OperationSpec>();
            for (;;)
            {
                var bodyLength = _converter.ToInt32(response, valueLengthOffset);
                var payLoad = new byte[bodyLength];
                Buffer.BlockCopy(response, valueOffset, payLoad, 0, bodyLength);

                var command = new OperationSpec
                {
                    Status = (ResponseStatus) _converter.ToUInt16(response, statusOffset),
                    ValueIsJson = payLoad.AsSpan().IsJson(),
                    Bytes = payLoad
                };
                operationSpecs.Add(command);

                statusOffset = valueOffset + bodyLength;
                valueLengthOffset = statusOffset + 2;
                valueOffset = statusOffset + 6;

                if (valueOffset >= response.Length) break;
            }

            var spec = operationSpecs[index];
            return _serializer.Deserialize<T>(spec.Bytes.AsMemory());
        }

        public T ContentAs<T>(int index, ITypeSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public bool Exists(int index)
        {
            throw new NotImplementedException();
        }

        public ResponseStatus OpCode(int index)
        {
            throw new NotImplementedException();
        }
    }
}
