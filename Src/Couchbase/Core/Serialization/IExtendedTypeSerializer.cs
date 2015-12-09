using System.Reflection;

namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// Provides an interface for serialization and deserialization of K/V pairs, with support for more
    /// advanced deserialization features.
    /// </summary>
    public interface IExtendedTypeSerializer : ITypeSerializer
    {
        /// <summary>
        /// Informs consumers what deserialization options this <see cref="IExtendedTypeSerializer"/> supports.
        /// </summary>
        SupportedDeserializationOptions SupportedDeserializationOptions { get; }

        /// <summary>
        /// Provides custom deserialization options.  Options not listed in <see cref="IExtendedTypeSerializer.SupportedDeserializationOptions"/>
        /// will be ignored.  If null, then defaults will be used.
        /// </summary>
        DeserializationOptions DeserializationOptions { get; set; }

        /// <summary>
        /// Get the name which will be used for a given member during serialization/deserialization.
        /// </summary>
        /// <param name="member">Returns the name of this member.</param>
        /// <returns>
        /// The name which will be used for a given member during serialization/deserialization,
        /// or null if if will not be serialized.
        /// </returns>
        string GetMemberName(MemberInfo member);
    }
}
