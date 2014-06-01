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

        [ConfigurationProperty("useSsl", DefaultValue = false, IsRequired = false)]
        public bool UseSsl
        {
            get { return (bool)this["useSsl"]; }
            set { this["useSsl"] = value; }
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion