#if NET45
using System.Configuration;

namespace Couchbase.Configuration.Client.Providers
{
    public class ServerResolverElement : ConfigurationElement
    {
        /// <summary>
        /// Gets or sets the server resolver type used to try and find server URIs.
        /// </summary>
        [ConfigurationProperty("type", IsRequired = true)]
        public string Type
        {
            get { return (string) this["type"]; }
            set { this["type"] = value; }
        }
    }
}

#endif