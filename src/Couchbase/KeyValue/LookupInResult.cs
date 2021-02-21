using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    internal class LookupInResult : ILookupInResult, ITypeSerializerProvider
    {
        private readonly IList<LookupInSpec> _specs;

        /// <inheritdoc />
        public ITypeSerializer Serializer { get; }

        internal LookupInResult(IList<LookupInSpec> specs, ulong cas, TimeSpan? expiry, ITypeSerializer typeSerializer, bool isDeleted = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (specs == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(specs));
            }
            if (typeSerializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(typeSerializer));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _specs = specs.OrderBy(spec => spec.OriginalIndex).ToList();
            Cas = cas;
            Expiry = expiry;
            Serializer = typeSerializer;
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
            return Serializer.Deserialize<T>(spec.Bytes);
        }

        public bool Exists(int index)
        {
            if (index < 0 || index >= _specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = _specs[index];
            return spec.Status == ResponseStatus.Success;
        }

        /// <inheritdoc />
        public int IndexOf(string path)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }

            for (var i = 0; i < _specs.Count; i++)
            {
                if (_specs[i].Path == path)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
