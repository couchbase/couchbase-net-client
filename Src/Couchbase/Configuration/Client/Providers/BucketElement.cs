using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Client.Providers
{
    public sealed class BucketElement : ConfigurationElement
    {
        [ConfigurationProperty("useSsl", DefaultValue = false, IsRequired = false)]
        public bool UseSsl
        {
            get { return (bool)this["useSsl"]; }
            set { this["useSsl"] = value; }
        }

        [ConfigurationProperty("name", DefaultValue = "default", IsRequired = false, IsKey = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("password", DefaultValue = "", IsRequired = false)]
        public string Password
        {
            get { return (string)this["password"]; }
            set { this["password"] = value; }
        }
    }
}
