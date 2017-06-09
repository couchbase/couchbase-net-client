#if NET45
using System.Configuration;
using Couchbase.IO;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// A configuration element for registering custom <see cref="IIOService"/>s.
    /// </summary>
    public class IOServiceElement : ConfigurationElement
    {
        public IOServiceElement()
        {
            Name = "default";
            Type = "Couchbase.IO.Services.SharedPooledIOService, Couchbase.NetClient";
        }

        /// <summary>
        /// Gets or sets the name of the custom <see cref="IIOService"/>
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
        /// Gets or sets the <see cref="Type"/> of the custom <see cref="IIOService"/>
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