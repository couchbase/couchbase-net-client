#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;

namespace Couchbase.Integrated.Transactions.Internal
{
    /// <summary>
    /// Wraps a given <see cref="ITypeSerializer"/> to make sure it is not reported as <see cref="IStreamingTypeDeserializer"/> to force block serialization.
    /// </summary>
    internal class NonStreamingSerializerWrapper : ITypeSerializer
    {
        private readonly ITypeSerializer _serializer;

        public NonStreamingSerializerWrapper(ITypeSerializer serializer)
        {
            _serializer = serializer;
        }

        /// <summary>
        /// Returns or initializes a non-streaming <see cref="ITypeSerializer"/> based on cluster DI.
        /// </summary>
        /// <param name="cluster">The initialized Cluster.</param>
        /// <returns>An <see cref="ITypeSerializer"/> instance that does not implement <see cref="IStreamingTypeDeserializer"/>.</returns>
        public static ITypeSerializer FromCluster(ICluster cluster)
        {
            var clusterTranscoder = cluster.ClusterServices.GetService(typeof(ITypeTranscoder)) as ITypeTranscoder;
            var clusterSerializer = clusterTranscoder?.Serializer ?? (cluster.ClusterServices.GetService(typeof(ITypeSerializer)) as ITypeSerializer);
            _ = clusterSerializer ?? throw new NotSupportedException($"{nameof(ITypeSerializer)} could not be resolved via Cluster Dependency Injection configuration.");
            if (clusterSerializer is IStreamingTypeDeserializer)
            {
                return new NonStreamingSerializerWrapper(clusterSerializer);
            }

            return clusterSerializer;
        }

        public T? Deserialize<T>(ReadOnlyMemory<byte> buffer) => _serializer.Deserialize<T>(buffer);

        public T? Deserialize<T>(Stream stream) => _serializer.Deserialize<T>(stream);

        public ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) => _serializer.DeserializeAsync<T>(stream, cancellationToken);

        public void Serialize(Stream stream, object? obj) => _serializer.Serialize(stream, obj);

        public ValueTask SerializeAsync(Stream stream, object? obj, CancellationToken cancellationToken = default) => _serializer.SerializeAsync(stream, obj, cancellationToken);
    }
}





