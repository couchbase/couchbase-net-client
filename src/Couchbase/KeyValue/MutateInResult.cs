using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    public class MutateInResult : IMutateInResult
    {
        private readonly IList<OperationSpec> _specs;
        private readonly ITypeSerializer _serializer;

        public MutateInResult(IList<OperationSpec> specs, ulong cas, MutationToken? token, ITypeSerializer serializer)
        {
            if (specs == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(specs));
            }
            if (serializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serializer));
            }

            _specs = specs.OrderBy(spec => spec.OriginalIndex).ToList();
            Cas = cas;
            MutationToken = token ?? MutationToken.Empty;
            _serializer = serializer;
        }

        public ulong Cas { get; }
        public MutationToken MutationToken { get; set; }
        public T ContentAs<T>(int index)
        {
            if (index < 0 || index >= _specs.Count)
            {
                ThrowHelper.ThrowInvalidIndexException($"The index provided is out of range: {index}.");
            }

            var spec = _specs[index];
            return _serializer.Deserialize<T>(spec.Bytes);
        }
    }
}
