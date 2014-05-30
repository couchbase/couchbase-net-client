using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Client.Providers
{
    public class ConnectionPoolElement : ConfigurationElement
    {
        [ConfigurationProperty("name", DefaultValue = "default", IsRequired = false, IsKey = true)]
        public string Name
        {
            get { return (string) this["name"]; }
            set { this["name"] = value; }
        }

        [ConfigurationProperty("maxSize", DefaultValue = 2, IsRequired = false)]
        public int MaxSize
        {
            get { return (int) this["maxSize"]; }
            set { this["maxSize"] = value; }
        }

        [ConfigurationProperty("minSize", DefaultValue = 1, IsRequired = false)]
        public int MinSize
        {
            get { return (int) this["minSize"]; }
            set { this["minSize"] = value; }
        }

        [ConfigurationProperty("waitTimeout", DefaultValue = 2500, IsRequired = false)]
        public int WaitTimeout
        {
            get { return (int)this["waitTimeout"]; }
            set { this["waitTimeout"] = value; }
        }

        [ConfigurationProperty("shutdownTimeout", DefaultValue = 10000, IsRequired = false)]
        public int ShutdownTimeout
        {
            get { return (int)this["shutdownTimeout"]; }
            set { this["shutdownTimeout"] = value; }
        }
    }
}
