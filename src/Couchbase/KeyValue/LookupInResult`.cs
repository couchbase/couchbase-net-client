using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Wrapper for a <see cref="ILookupInResult"/> which adds a known document type.
    /// </summary>
    internal class LookupInResult<TDocument> : ILookupInResult<TDocument>
    {
        private readonly ILookupInResult _inner;

        public LookupInResult(ILookupInResult inner)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (inner == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(inner));
            }
            if (inner is not ITypeSerializerProvider)
            {
                ThrowHelper.ThrowNotSupportedException("The class implementing ILookupInResult must also implement ITypeSerializerProvider.");
            }

            _inner = inner;
        }

        /// <inheritdoc />
        public ulong Cas => _inner.Cas;

        /// <inheritdoc />
        public bool Exists(int index) => _inner.Exists(index);

        /// <inheritdoc />
        public bool IsDeleted => _inner.IsDeleted;

        /// <inheritdoc />
        public T ContentAs<T>(int index) => _inner.ContentAs<T>(index);

        /// <inheritdoc />
        public int IndexOf(string path) => _inner.IndexOf(path);

        public ITypeSerializer Serializer => ((ITypeSerializerProvider) _inner).Serializer;
    }
}
