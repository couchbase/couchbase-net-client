#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Implementation of <see cref="IFallbackTypeSerializerProvider"/> which provides a <see cref="DefaultSerializer"/>.
    /// </summary>
    internal class DefaultFallbackTypeSerializerProvider : IFallbackTypeSerializerProvider
    {
        private static DefaultFallbackTypeSerializerProvider? _instance;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static DefaultFallbackTypeSerializerProvider Instance => _instance ??= new DefaultFallbackTypeSerializerProvider();

        /// <inheritdoc />
        public ITypeSerializer Serializer => DefaultSerializer.Instance;
    }
}
