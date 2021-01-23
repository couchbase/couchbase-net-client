using Couchbase.Core;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Wrapper for a <see cref="IMutateInResult"/> which adds a known document type.
    /// </summary>
    internal class MutateInResult<TDocument> : IMutateInResult<TDocument>
    {
        private readonly IMutateInResult _inner;

        public MutateInResult(IMutateInResult inner)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (inner == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(inner));
            }
            if (inner is not ITypeSerializerProvider)
            {
                ThrowHelper.ThrowNotSupportedException("The class implementing IMutateInResult must also implement ITypeSerializerProvider.");
            }

            _inner = inner;
        }

        /// <inheritdoc />
        public ulong Cas => _inner.Cas;

        /// <inheritdoc />
        public MutationToken MutationToken
        {
            get => _inner.MutationToken;
            set => _inner.MutationToken = value;
        }

        /// <inheritdoc />
        public ITypeSerializer Serializer => ((ITypeSerializerProvider) _inner).Serializer;

        /// <inheritdoc />
        public T ContentAs<T>(int index) => _inner.ContentAs<T>(index);

        /// <inheritdoc />
        public int IndexOf(string path) => _inner.IndexOf(path);
    }
}
