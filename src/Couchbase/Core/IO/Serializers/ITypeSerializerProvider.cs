namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Provides access to an <see cref="ITypeSerializer"/> related to the object.
    /// </summary>
    public interface ITypeSerializerProvider
    {
        /// <summary>
        /// Gets the <see cref="ITypeSerializer"/> related to the object.
        /// </summary>
        ITypeSerializer Serializer { get; }
    }
}
