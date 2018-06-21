#if NET452
using System;
using System.Configuration;
using Couchbase.IO.Operations;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Allows a Couchbase Server's Bucket to be configured.
    /// </summary>
    public sealed class BucketElement : ConfigurationElement, IBucketDefinition
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use enhanced durability if the
        /// Couchbase server version supports it; if it's not supported the client will use
        /// Observe for Endure operations.
        /// </summary>
        /// <value>
        /// <c>true</c> to use enhanced durability; otherwise, <c>false</c>.
        /// </value>
        [ConfigurationProperty("useEnhancedDurability", DefaultValue = false, IsRequired = false)]
        public bool UseEnhancedDurability
        {
            get { return (bool)this["useEnhancedDurability"]; }
            set { this["useEnhancedDurability"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to get an <see cref="ErrorMap"/> to get additional error information
        /// for unknown errors returned from the server.
        /// </summary>
        /// <value>
        /// <c>true</c> to use kv error map; otherwise, <c>false</c>.
        /// </value>
        [Obsolete("KV Error Map are always enabled if available.")]
        [ConfigurationProperty("useKvErrorMap", DefaultValue = false, IsRequired = false)]
        public bool UseKvErrorMap
        {
            get { return (bool)this["useKvErrorMap"]; }
            set { this["useKvErrorMap"] = value; }
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
        /// The name of the Bucket.
        /// </summary>
        /// <remarks>The name can be set within the Couchbase Management Console.</remarks>
        [ConfigurationProperty("name", DefaultValue = "default", IsRequired = false, IsKey = true)]
        public string Name
        {
            get { return (string)this["name"]; }
            set { this["name"] = value; }
        }

        /// <summary>
        /// The password used to connect to the bucket.
        /// </summary>
        /// <remarks>The password can be set within the Couchbase Management Console.</remarks>
        [ConfigurationProperty("password", DefaultValue = "", IsRequired = false)]
        public string Password
        {
            get { return (string)this["password"]; }
            set { this["password"] = value; }
        }

        /// <summary>
        /// Allows the default connection pool settings to be overridden.
        /// </summary>
        /// <remarks>The default settings are: MinSize=1, MaxSize=2, WaitTimout=2500ms, ShutdownTimeout=10000ms.</remarks>
        [ConfigurationProperty("connectionPool", IsRequired = false)]
        public ConnectionPoolElement ConnectionPool
        {
            get { return (ConnectionPoolElement) this["connectionPool"]; }
            set { this["connectionPool"] = value; }
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

        /// <summary>
        /// Gets or sets the operation lifespan, maximum time in milliseconds allowed for an operation to run.
        /// </summary>
        [ConfigurationProperty("operationLifespan", DefaultValue = null, IsRequired = false)]
        public uint? OperationLifespan
        {
            get { return (uint?)this["operationLifespan"]; }
            set { this["operationLifespan"] = value; }
        }

        #region Additional IBucketDefinition Implementations

        IConnectionPoolDefinition IBucketDefinition.ConnectionPool
        {
            get { return ConnectionPool.ElementInformation.IsPresent ? ConnectionPool : null; }
        }

        #endregion
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
