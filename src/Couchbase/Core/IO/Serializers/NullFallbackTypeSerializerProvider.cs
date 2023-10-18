#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// For use with trimming, does not provide a fallback serializer.
    /// </summary>
    internal class NullFallbackTypeSerializerProvider : IFallbackTypeSerializerProvider
    {
        private static NullFallbackTypeSerializerProvider? _instance;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static NullFallbackTypeSerializerProvider Instance => _instance ??= new NullFallbackTypeSerializerProvider();

        /// <inheritdoc />
        public ITypeSerializer? Serializer => null;
    }
}
