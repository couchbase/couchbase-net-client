using System;
using System.Collections.Generic;
using System.Linq;
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

        internal LookupInResult(IList<OperationSpec> specs, ulong cas, TimeSpan? expiry, ITypeSerializer typeSerializer, bool isDeleted = false)
        {
            var reOrdered = specs ?? throw new ArgumentNullException(nameof(specs));
            _specs = reOrdered.OrderBy(spec => spec.OriginalIndex).ToList();
            Cas = cas;
            Expiry = expiry;
            _serializer = typeSerializer ?? throw new ArgumentNullException(nameof(typeSerializer));
            IsDeleted = isDeleted;
        }

        public ulong Cas { get; }

        public TimeSpan? Expiry { get; }

        public bool IsDeleted { get; }

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
