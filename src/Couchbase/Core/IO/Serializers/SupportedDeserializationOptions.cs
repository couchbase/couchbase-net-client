
namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    ///     Supplied by <see cref="IExtendedTypeSerializer" /> to define which deserialization options it supports.
    /// </summary>
    /// <remarks>Intended to help support backwards compatibility as new deserialization options are added in the future.</remarks>
    public class SupportedDeserializationOptions
    {
        /// <summary>
        ///     If true, the <see cref="IExtendedTypeSerializer" /> supports
        ///     <see cref="DeserializationOptions.CustomObjectCreator" />.
        /// </summary>
        public bool CustomObjectCreator { get; set; }
    }
}
