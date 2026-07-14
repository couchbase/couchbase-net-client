using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// An <see cref="ITypeSerializer"/> decorator that treats <see langword="byte"/>[] as a raw
    /// pass-through in both directions and delegates every other type to an inner serializer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default serializers (Newtonsoft and System.Text.Json) encode a <see langword="byte"/>[]
    /// as a Base64 JSON string, so a plain <c>ContentAs&lt;byte[]&gt;()</c> Base64-decodes. That is
    /// required for round-tripping arbitrary binary through data structures such as
    /// <c>PersistentQueue&lt;byte[]&gt;</c>. When you instead want the <em>raw</em> encoded bytes of a
    /// value (for example, the raw JSON of a sub-document fragment), wrap the configured serializer in
    /// this decorator and supply it via a per-operation transcoder. This matches the behaviour of the
    /// Java SDK's default serializer, where <c>byte[]</c> is always raw pass-through.
    /// </para>
    /// <para>
    /// Because non-<see langword="byte"/>[] types are delegated to the inner serializer, its JSON
    /// semantics (converters, naming policy, AOT source generation, etc.) are preserved. This type
    /// implements only <see cref="ITypeSerializer"/>; it does not forward the extended serializer
    /// interfaces (projections, streaming), so it is intended for per-operation use (e.g. a raw
    /// <see langword="byte"/>[] sub-document read) rather than as a cluster-wide serializer.
    /// </para>
    /// </remarks>
    public sealed class RawByteArraySerializer : ITypeSerializer
    {
        private readonly ITypeSerializer _inner;

        /// <summary>
        /// Creates a <see cref="RawByteArraySerializer"/> wrapping the serializer that should handle
        /// all non-<see langword="byte"/>[] types.
        /// </summary>
        /// <param name="inner">The serializer to delegate to for non-<see langword="byte"/>[] types.</param>
        public RawByteArraySerializer(ITypeSerializer inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <inheritdoc />
        public T? Deserialize<T>(ReadOnlyMemory<byte> buffer) =>
            typeof(T) == typeof(byte[]) ? (T)(object)buffer.ToArray() : _inner.Deserialize<T>(buffer);

        /// <inheritdoc />
        public T? Deserialize<T>(Stream stream)
        {
            if (typeof(T) == typeof(byte[]))
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return (T)(object)ms.ToArray();
            }

            return _inner.Deserialize<T>(stream);
        }

        /// <inheritdoc />
        public async ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            if (typeof(T) == typeof(byte[]))
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, 81920, cancellationToken).ConfigureAwait(false);
                return (T)(object)ms.ToArray();
            }

            return await _inner.DeserializeAsync<T>(stream, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public void Serialize(Stream stream, object? obj)
        {
            if (obj is byte[] bytes)
            {
                stream.Write(bytes, 0, bytes.Length);
                return;
            }

            _inner.Serialize(stream, obj);
        }

        /// <inheritdoc />
        public async ValueTask SerializeAsync(Stream stream, object? obj, CancellationToken cancellationToken = default)
        {
            if (obj is byte[] bytes)
            {
                await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                return;
            }

            await _inner.SerializeAsync(stream, obj, cancellationToken).ConfigureAwait(false);
        }
    }
}
