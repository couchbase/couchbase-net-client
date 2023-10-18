#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Service which provides a fallback serializer for special cases. The typical implementation is the
    /// <see cref="DefaultFallbackTypeSerializerProvider"/> which provides a <see cref="DefaultSerializer"/>.
    /// </summary>
    internal interface IFallbackTypeSerializerProvider
    {
        /// <summary>
        /// The fallback serializer, or null if none is available.
        /// </summary>
        ITypeSerializer? Serializer { get; }
    }
}
