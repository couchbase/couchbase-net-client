namespace Couchbase.Core.Serialization
{
    /// <summary>
    /// Options to control deserialization process in an <see cref="IExtendedTypeSerializer"/>.
    /// </summary>
    public class DeserializationOptions
    {
        /// <summary>
        /// Returns true if any custom options are set
        /// </summary>
        public bool HasSettings
        {
            get
            {
                return CustomObjectCreator != null;
            }
        }

        /// <summary>
        /// <see cref="ICustomObjectCreator"/> to use when creating objects during deserialization.
        /// Null will uses the <see cref="IExtendedTypeSerializer"/> defaults for type creation.
        /// </summary>
        public ICustomObjectCreator CustomObjectCreator { get; set; }
    }
}
