using System;
using System.Collections.Generic;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class LookupInResult : ILookupInResult
    {
        private readonly IList<OperationSpec> _specs;
        private readonly ITypeSerializer _serializer;

        internal LookupInResult(IList<OperationSpec> specs, ulong cas, TimeSpan? expiry, ITypeSerializer typeSerializer)
        {
            _specs = specs;
            Cas = cas;
            Expiry = expiry;
            _serializer = typeSerializer ?? throw new ArgumentNullException(nameof(typeSerializer));
        }

        public ulong Cas { get; }

        public TimeSpan? Expiry { get; }

        public T ContentAs<T>(int index)
        {
            if (index < 0 || index >= _specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = _specs[index];
            return _serializer.Deserialize<T>(spec.Bytes);
        }

        public bool Exists(int index)
        {
            if (index < 0 || index >= _specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = _specs[index];
            return spec.Status == ResponseStatus.SubDocPathNotFound;
        }
    }
}
