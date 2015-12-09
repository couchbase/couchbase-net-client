namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// A configuration element for registering custom <see cref="ITypeSerializer"/>s.
    /// </summary>
    public sealed class SerializerElement
    {
        public SerializerElement()
        {
            Name = "default";
            Type = "Couchbase.Core.Serialization.DefaultSerializer, Couchbase.NetClient";
        }

        /// <summary>
        /// Gets or sets the name of the custom <see cref="ITypeSerializer"/>
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Type"/> of the custom <see cref="ITypeSerializer"/>
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public string Type { get; set; }
    }
}
