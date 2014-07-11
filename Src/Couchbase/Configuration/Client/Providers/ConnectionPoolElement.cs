using System.Configuration;
using Couchbase.IO;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Provides configuration support for the Bucket's <see cref="ConnectionPool{T}"/> object, which is pool of TCP connections.
    /// </summary>
    public class ConnectionPoolElement : ConfigurationElement
    {
        /// <summary>
        /// The name for the connection pool. 
        /// </summary>
        /// <remarks>This is used internally and does not need to be set or customized.</remarks>
        [ConfigurationProperty("name", DefaultValue = "default", IsRequired = false, IsKey = true)]
        public string Name
        {
            get { return (string) this["name"]; }
            set { this["name"] = value; }
        }

        /// <summary>
        /// The maximum number of TCP connections that the client will allocate for a given Bucket.
        /// </summary>
        /// <remarks>The default is two TCP connections per bucket.</remarks>
        [ConfigurationProperty("maxSize", DefaultValue = 2, IsRequired = false)]
        public int MaxSize
        {
            get { return (int) this["maxSize"]; }
            set { this["maxSize"] = value; }
        }

        /// <summary>
        /// The minimum number of TCP connections that the client will allocate for a given bucket.
        /// </summary>
        /// <remarks>The default is one TCP connection per bucket.</remarks>
        /// <remarks>The connection pool will add TCP connections until <see cref="MaxSize"/> is reached.</remarks>
        [ConfigurationProperty("minSize", DefaultValue = 1, IsRequired = false)]
        public int MinSize
        {
            get { return (int) this["minSize"]; }
            set { this["minSize"] = value; }
        }

        /// <summary>
        /// The amount of time a thread will wait for a <see cref="IConnection"/> once the MaxSize of the pool has been reached and no TCP connections are available.
        /// </summary>
        /// <remarks>The default is 2500ms.</remarks>
        [ConfigurationProperty("waitTimeout", DefaultValue = 2500, IsRequired = false)]
        public int WaitTimeout
        {
            get { return (int)this["waitTimeout"]; }
            set { this["waitTimeout"] = value; }
        }

        /// <summary>
        /// Wait up to the <see cref="ShutdownTimeout"/> to send or recieve data before closing the <see cref="IConnection"/>.
        /// </summary>
        /// <remarks>The default value is 10000ms.</remarks>
        [ConfigurationProperty("shutdownTimeout", DefaultValue = 10000, IsRequired = false)]
        public int ShutdownTimeout
        {
            get { return (int)this["shutdownTimeout"]; }
            set { this["shutdownTimeout"] = value; }
        }

        /// <summary>
        /// Set to true to use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>If the parent <see cref="CouchbaseClientSection"/>'s UseSSL is false, setting this to true will override that configuration and enable the Bucket to use SSL./></remarks>
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
