using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Client.Providers
{
    public sealed class CouchbaseClientSection : ConfigurationSection
    {
        [ConfigurationProperty("useSsl", DefaultValue = false, IsRequired = false)]
        public bool UseSsl
        {
            get { return (bool) this["useSsl"]; }
            set { this["useSsl"] = value; }
        }

        [ConfigurationProperty("servers", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(UriElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public UriElementCollection Servers
        {
            get { return (UriElementCollection) this["servers"]; }
            set { this["servers"] = value; }
        } 

        [ConfigurationProperty("buckets", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(BucketElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public BucketElementCollection Buckets
        {
            get { return (BucketElementCollection) this["buckets"]; }
            set { this["buckets"] = value; }
        }

        [ConfigurationProperty("sslPort", DefaultValue = 11207, IsRequired = false)]
        public int SslPort
        {
            get { return (int)this["sslPort"]; }
            set { this["sslPort"] = value; }
        }

        [ConfigurationProperty("apiPort", DefaultValue = 8092, IsRequired = false)]
        public int ApiPort
        {
            get { return (int)this["apiPort"]; }
            set { this["apiPort"] = value; }
        }

        [ConfigurationProperty("mgmtPort", DefaultValue = 8091, IsRequired = false)]
        public int MgmtPort
        {
            get { return (int)this["mgmtPort"]; }
            set { this["mgmtPort"] = value; }
        }

        [ConfigurationProperty("directPort", DefaultValue = 11210, IsRequired = false)]
        public int DirectPort
        {
            get { return (int)this["directPort"]; }
            set { this["directPort"] = value; }
        }

        [ConfigurationProperty("httpsMgmtPort", DefaultValue = 18091, IsRequired = false)]
        public int HttpsMgmtPort
        {
            get { return (int)this["httpsMgmtPort"]; }
            set { this["httpsMgmtPort"] = value; }
        }

        [ConfigurationProperty("httpsApiPort", DefaultValue = 18092, IsRequired = false)]
        public int HttpsApiPort
        {
            get { return (int)this["httpsApiPort"]; }
            set { this["httpsApiPort"] = value; }
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