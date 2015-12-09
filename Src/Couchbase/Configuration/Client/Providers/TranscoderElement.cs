namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// A configuration element for registering custom <see cref="ITypeTranscoder"/>s.
    /// </summary>
    public sealed class TranscoderElement
    {
        public TranscoderElement()
        {
            Name = "default";
            Type = "Couchbase.Core.Transcoders.DefaultTranscoder, Couchbase.NetClient";
        }

        /// <summary>
        /// Gets or sets the name of the custom <see cref="ITypeTranscoder"/>
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }


        /// <summary>
        /// Gets or sets the <see cref="Type"/> of the custom <see cref="ITypeTranscoder"/>
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        public string Type { get; set; }

    }
}
