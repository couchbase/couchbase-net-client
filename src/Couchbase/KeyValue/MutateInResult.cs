using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Operations.SubDocument;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    public class MutateInResult : IMutateInResult, ITypeSerializerProvider
    {
        private readonly IList<OperationSpec> _specs;

        /// <inheritdoc />
        public ITypeSerializer Serializer { get; }

        public MutateInResult(IList<OperationSpec> specs, ulong cas, MutationToken? token, ITypeSerializer serializer)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (specs == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(specs));
            }
            if (serializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serializer));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _specs = specs.OrderBy(spec => spec.OriginalIndex).ToList();
            Cas = cas;
            MutationToken = token ?? MutationToken.Empty;
            Serializer = serializer;
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
            return Serializer.Deserialize<T>(spec.Bytes);
        }

        /// <inheritdoc />
        [InterfaceStability(Level.Volatile)]
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
