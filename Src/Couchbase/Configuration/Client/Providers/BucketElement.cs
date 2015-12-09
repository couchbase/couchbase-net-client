namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Allows a Couchbase Server's Bucket to be configured.
    /// </summary>
    public sealed class BucketElement
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use enhanced durability if the
        /// Couchbase server version supports it; if it's not supported the client will use
        /// Observe for Endure operations.
        /// </summary>
        /// <value>
        /// <c>true</c> to use enhanced durability; otherwise, <c>false</c>.
        /// </value>
        public bool UseEnhancedDurability {get; set; } = false;
        
        /// <summary>
        /// Set to true to use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>If the parent <see cref="CouchbaseClientSection"/>'s UseSSL is false, setting this to true will override that configuration and enable the Bucket to use SSL./></remarks>
        public bool UseSsl {get; set; } = false;

        /// <summary>
        /// The name of the Bucket.
        /// </summary>
        /// <remarks>The name can be set within the Couchbase Management Console.</remarks>
        public string Name {get; set; } = "default";

        /// <summary>
        /// The password used to connect to the bucket. 
        /// </summary>
        /// <remarks>The password can be set within the Couchbase Management Console.</remarks>
        public string Password {get; set; } = "";

        /// <summary>
        /// Allows the default connection pool settings to be overridden. 
        /// </summary>
        /// <remarks>The default settings are: MinSize=1, MaxSize=2, WaitTimeout=2500ms, ShutdownTimeout=10000ms.</remarks>
        public ConnectionPoolElement ConnectionPool {get; set; } = new ConnectionPoolElement { MinSize = 1, MaxSize=2, WaitTimeout=2500, ShutdownTimeout=10000 };

        /// <summary>
        /// Gets or Sets the max time an observe operation will take before timing out.
        /// </summary>
        public int ObserveInterval {get; set; } = 2;

        /// <summary>
        /// Gets or Sets the interval between each observe attempt.
        /// </summary>
        public int ObserveTimeout {get; set; } = 500;

        /// <summary>
        /// Gets or sets the operation lifespan, maximum time in milliseconds allowed for an operation to run.
        /// </summary>
        public uint? OperationLifespan {get; set; } = null;
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
