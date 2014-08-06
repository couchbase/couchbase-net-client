using System.Configuration;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Allows the Client Configuration to be set through an App.config or a Web.config. 
    /// </summary>
    public sealed class CouchbaseClientSection : ConfigurationSection
    {
        /// <summary>
        /// Set to true to use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>Set to true to require all buckets to use SSL.</remarks>
        /// <remarks>Set to false and then set UseSSL at the individual Bucket level to use SSL on specific buckets.</remarks>
        [ConfigurationProperty("useSsl", DefaultValue = false, IsRequired = false)]
        public bool UseSsl
        {
            get { return (bool) this["useSsl"]; }
            set { this["useSsl"] = value; }
        }

        /// <summary>
        /// Sets the Couchbase Server's list of bootstrap URI's. The client will use the list to connect to initially connect to the cluster.
        /// </summary>
        [ConfigurationProperty("servers", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(UriElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public UriElementCollection Servers
        {
            get { return (UriElementCollection) this["servers"]; }
            set { this["servers"] = value; }
        } 

        /// <summary>
        /// Allows specific configurations of Bucket's to be defined, overriding the parent's settings.
        /// </summary>
        [ConfigurationProperty("buckets", IsDefaultCollection = true)]
        [ConfigurationCollection(typeof(BucketElementCollection), AddItemName = "add", ClearItemsName = "clear", RemoveItemName = "remove")]
        public BucketElementCollection Buckets
        {
            get { return (BucketElementCollection) this["buckets"]; }
            set { this["buckets"] = value; }
        }

        /// <summary>
        /// Overrides the default and sets the SSL port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 11207.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom SSL port.</remarks>
        [ConfigurationProperty("sslPort", DefaultValue = 11207, IsRequired = false)]
        public int SslPort
        {
            get { return (int)this["sslPort"]; }
            set { this["sslPort"] = value; }
        }

        /// <summary>
        /// Overrides the default and sets the Views REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Views REST API port.</remarks>
        [ConfigurationProperty("apiPort", DefaultValue = 8092, IsRequired = false)]
        public int ApiPort
        {
            get { return (int)this["apiPort"]; }
            set { this["apiPort"] = value; }
        }

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Management REST API port.</remarks>
        [ConfigurationProperty("mgmtPort", DefaultValue = 8091, IsRequired = false)]
        public int MgmtPort
        {
            get { return (int)this["mgmtPort"]; }
            set { this["mgmtPort"] = value; }
        }

        /// <summary>
        /// Overrides the default and sets the direct port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        /// <remarks>The default and suggested direct port is 11210.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom direct port.</remarks>
        [ConfigurationProperty("directPort", DefaultValue = 11210, IsRequired = false)]
        public int DirectPort
        {
            get { return (int)this["directPort"]; }
            set { this["directPort"] = value; }
        }

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Management REST API SSL port.</remarks>
        [ConfigurationProperty("httpsMgmtPort", DefaultValue = 18091, IsRequired = false)]
        public int HttpsMgmtPort
        {
            get { return (int)this["httpsMgmtPort"]; }
            set { this["httpsMgmtPort"] = value; }
        }

        /// <summary>
        /// Overrides the default and sets the Couchbase Views REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Views REST API SSL port.</remarks>
        [ConfigurationProperty("httpsApiPort", DefaultValue = 18092, IsRequired = false)]
        public int HttpsApiPort
        {
            get { return (int)this["httpsApiPort"]; }
            set { this["httpsApiPort"] = value; }
        }

        /// <summary>
        /// Gets or Sets the max time an observe operation will take before timing out.
        /// </summary>
        [ConfigurationProperty("observeInterval", DefaultValue = 2, IsRequired = false)]
        public int ObserveInterval
        {
            get { return (int)this["observeInterval"]; }
            set { this["observeInterval"] = value; }
        }

        /// <summary>
        /// Gets or Sets the interval between each observe attempt.
        /// </summary>
        [ConfigurationProperty("observeTimeout", DefaultValue = 500, IsRequired = false)]
        public int ObserveTimeout
        {
            get { return (int)this["observeTimeout"]; }
            set { this["observeTimeout"] = value; }
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