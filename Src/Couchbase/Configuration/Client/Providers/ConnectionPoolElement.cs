#if NET452
using System.Configuration;
using Couchbase.IO;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Provides configuration support for the Bucket's <see cref="ConnectionPool{T}"/> object, which is pool of TCP connections.
    /// </summary>
    public class ConnectionPoolElement : ConfigurationElement, IConnectionPoolDefinition
    {
        private const string DefaultTypeName =
            "Couchbase.IO.SharedConnectionPool`1[Couchbase.IO.MultiplexingConnection], Couchbase.NetClient";

        private const string DefaultSslTypeName =
           "Couchbase.IO.ConnectionPool`1[Couchbase.IO.SslConnection], Couchbase.NetClient";

        /// <summary>
        /// Enables X509 authentication with the Couchbase cluster.
        /// </summary>
        [ConfigurationProperty("enableCertificateAuthentication", DefaultValue = false, IsRequired = false)]
        public bool EnableCertificateAuthentication
        {
            get => (bool)this["enableCertificateAuthentication"];
            set => this["enableCertificateAuthentication"] = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Type"/> of the custom <see cref="IConnectionPool"/>
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        [ConfigurationProperty("type", DefaultValue = DefaultTypeName, IsRequired = false, IsKey = false)]
        public string Type
        {
            get
            {
                var typeName = (string) this["type"];

                //if ssl is enabled and no custom type is being used, default to the SslConnection class.
                if (UseSsl && typeName.Equals(DefaultTypeName))
                {
                    typeName = DefaultSslTypeName;
                }
                return typeName;
            }
            set { this["type"] = value; }
        }

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
        /// Cancels a pending operation if it does not complete in the time given and marks the connection as dead.
        /// </summary>
        /// <remarks>The default value is 15000ms</remarks>
        [ConfigurationProperty("sendTimeout", DefaultValue = 15000, IsRequired = false)]
        public int SendTimeout
        {
            get { return (int) this["sendTimeout"]; }
            set { this["sendTimeout"] = value; }
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

        /// <summary>
        /// The size of each buffer to allocate per TCP connection for sending and recieving Memcached operations
        /// </summary>
        /// <remarks>The default is 16K</remarks>
        /// <remarks>The total buffer size is BufferSize * PoolConfiguration.MaxSize</remarks>
        [ConfigurationProperty("bufferSize", DefaultValue = 1024 * 16, IsRequired = false)]
        public int BufferSize
        {
            get { return (int)this["bufferSize"]; }
            set { this["bufferSize"] = value; }
        }

        /// <summary>
        /// The amount time allotted for the client to establish a TCP connection with a server before failing
        /// </summary>
        /// <remarks>The default value is 10000ms.</remarks>
        [ConfigurationProperty("connectTimeout", DefaultValue = 10000, IsRequired = false)]
        public int ConnectTimeout
        {
            get { return (int)this["connectTimeout"]; }
            set { this["connectTimeout"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether enable TCP keep alives.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable TCP keep alives; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>The default is true; TCP Keep Alives are enabled.</remarks>
        [ConfigurationProperty("enableTcpKeepAlives", DefaultValue = true, IsRequired = false)]
        public bool EnableTcpKeepAlives
        {
            get { return (bool)this["enableTcpKeepAlives"]; }
            set { this["enableTcpKeepAlives"] = value; }
        }

        /// <summary>
        /// Specifies the timeout, in milliseconds, with no activity until the first keep-alive packet is sent.
        /// </summary>
        /// <value>
        /// The TCP keep alive time in milliseconds.
        /// </value>
        /// <remarks>The default is 2hrs.</remarks>
        [ConfigurationProperty("tcpKeepAliveTime", DefaultValue = ((uint)2 * 60 * 60 * 1000), IsRequired = false)]
        public uint TcpKeepAliveTime
        {
            get { return (uint)this["tcpKeepAliveTime"]; }
            set { this["tcpKeepAliveTime"] = value; }
        }

        /// <summary>
        /// Specifies the interval, in milliseconds, between when successive keep-alive packets are sent if no acknowledgement is received.
        /// </summary>
        /// <value>
        /// The TCP keep alive interval in milliseconds..
        /// </value>
        /// <remarks>The default is 1 second.</remarks>
        [ConfigurationProperty("tcpKeepAliveInterval", DefaultValue = ((uint)1000), IsRequired = false)]
        public uint TcpKeepAliveInterval
        {
            get { return (uint)this["tcpKeepAliveInterval"]; }
            set { this["tcpKeepAliveInterval"] = value; }
        }

        /// <summary>
        /// Gets or sets the maximum number of times the client will try to close a <see cref="IConnection"/>
        /// if it's in use and <see cref="IConnectionPool"/> has been disposed.
        /// </summary>
        /// <value>
        /// The maximum close attempts.
        /// </value>
        [ConfigurationProperty("maxCloseAttempts", DefaultValue = (int)5, IsRequired = false)]
        public int MaxCloseAttempts
        {
            get { return (int)this["maxCloseAttempts"]; }
            set { this["maxCloseAttempts"] = value; }
        }

        /// <summary>
        /// Gets or sets the interval between close attempts on a <see cref="IConnection"/>
        /// if it's in use and <see cref="IConnectionPool"/> has been disposed.
        /// </summary>
        /// <value>
        /// The close attempt interval.
        /// </value>
        [ConfigurationProperty("closeAttemptInterval", DefaultValue = ((uint)100), IsRequired = false)]
        public uint CloseAttemptInterval
        {
            get { return (uint)this["closeAttemptInterval"]; }
            set { this["closeAttemptInterval"] = value; }
        }

        public override bool IsReadOnly()
        {
            return false;
        }
    }
}

#endif

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
