using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Client.Providers
{
    public sealed class UriElement : ConfigurationElement
    {
        [ConfigurationProperty("uri", IsRequired = true, IsKey = true)]
        public Uri Uri
        {
            get { return (Uri) this["uri"]; }
            set { this["uri"] = value; }
        }
    }
}
