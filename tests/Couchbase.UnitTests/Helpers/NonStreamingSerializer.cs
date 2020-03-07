using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.UnitTests.Helpers
{
    /// <summary>
    /// Implementation of <see cref="DefaultSerializer"/> which doesn't support the <see cref="IStreamingTypeDeserializer"/> interface.
    /// </summary>
    internal class NonStreamingSerializer : ITypeSerializer
    {
        private readonly ITypeSerializer _internalSerializer = new DefaultSerializer();

        public T Deserialize<T>(ReadOnlyMemory<byte> buffer)
        {
            return _internalSerializer.Deserialize<T>(buffer);
        }

        public T Deserialize<T>(Stream stream)
        {
            return _internalSerializer.Deserialize<T>(stream);
        }

        public async ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            return await _internalSerializer.DeserializeAsync<T>(stream, cancellationToken).ConfigureAwait(false);
        }

        public void Serialize(Stream stream, object obj)
        {
            _internalSerializer.Serialize(stream, obj);
        }

        public async ValueTask SerializeAsync(Stream stream, object obj, CancellationToken cancellationToken = default)
        {
            await _internalSerializer.SerializeAsync(stream, obj, cancellationToken).ConfigureAwait(false);
        }
    }
}
