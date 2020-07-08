using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.KeyValue
{
    public class MutateInResult : IMutateInResult
    {
        private readonly IList<OperationSpec> _specs;
        private readonly ITypeSerializer _serializer;

        public MutateInResult(IList<OperationSpec> specs, ulong cas, MutationToken? token, ITypeSerializer serializer)
        {
            var reOrdered  = specs ?? throw new ArgumentNullException(nameof(specs));
            _specs = reOrdered.OrderBy(spec => spec.OriginalIndex).ToList();
            Cas = cas;
            MutationToken = token ?? MutationToken.Empty;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public ulong Cas { get; }
        public MutationToken MutationToken { get; set; }
        public T ContentAs<T>(int index)
        {
            if (index < 0 || index >= _specs.Count)
            {
                throw new InvalidIndexException($"The index provided is out of range: {index}.");
            }
            var spec = _specs[index];
            return _serializer.Deserialize<T>(spec.Bytes);
        }
    }
}
