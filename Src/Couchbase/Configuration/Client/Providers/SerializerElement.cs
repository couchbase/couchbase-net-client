#if NET45
using System.Configuration;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// A configuration element for registering custom <see cref="ITypeSerializer"/>s.
    /// </summary>
    public sealed class SerializerElement : ConfigurationElement
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
        [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        /// <summary>
        /// Gets or sets the <see cref="Type"/> of the custom <see cref="ITypeSerializer"/>
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        [ConfigurationProperty("type", IsRequired = true, IsKey = false)]
        public string Type
        {
            get { return (string)this["type"]; }
            set { this["type"] = value; }
        }
    }
}

#endif