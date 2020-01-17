using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.DataMapping
{

    /// <summary>
    /// A class for mapping an input stream of JSON to a Type T using a <see cref="Newtonsoft.Json.JsonTextReader"/> instance.
    /// </summary>
    internal class JsonDataMapper : IDataMapper
    {
        private readonly ITypeSerializer _serializer;

        public JsonDataMapper(ITypeSerializer serializer)
        {
            _serializer = serializer;
        }

        /// <inheritdoc />
        public T Map<T>(Stream stream) where T : class
        {
            return _serializer.Deserialize<T>(stream);
        }

        /// <inheritdoc />
        public ValueTask<T> MapAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class
        {
            return _serializer.DeserializeAsync<T>(stream, cancellationToken);
        }
    }
}
