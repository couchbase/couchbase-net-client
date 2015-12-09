namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// A configuration element for registering custom <see cref="IByteConverter"/>s.
    /// </summary>
    public sealed class ConverterElement
    {
        public ConverterElement()
        {
            Name = "default";
            Type = "Couchbase.IO.Converters.DefaultConverter, Couchbase.NetClient";
        }

        /// <summary>
        /// Gets or sets the name of the custom <see cref="IByteConverter"/>
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Type"/> of the custom <see cref="IByteConverter"/>
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public string Type { get; set; }
    }
}
