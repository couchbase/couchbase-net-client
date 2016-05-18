using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// POCO configuration definition which defines the settings for a bucket and can be
    /// used to deserialize a configuration file using JSON, XML, or other configuration formats.
    /// It is used to construct a <see cref="ClientConfiguration"/> as part of a <see cref="CouchbaseClientDefinition"/>.
    /// </summary>
    public class BucketDefinition : IBucketDefinition
    {
        /// <summary>
        /// A value indicating whether to use enhanced durability if the
        /// Couchbase server version supports it; if it's not supported the client will use
        /// Observe for Endure operations.
        /// </summary>
        /// <value>
        /// <c>true</c> to use enhanced durability; otherwise, <c>false</c>.
        /// </value>
        public bool UseEnhancedDurability { get; set; }

        /// <summary>
        /// If true, use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>If the parent <see cref="ICouchbaseClientDefinition"/>'s UseSSL is false, setting this to true will override that configuration and enable the Bucket to use SSL.</remarks>
        public bool UseSsl { get; set; }

        /// <summary>
        /// The name of the Bucket.
        /// </summary>
        /// <remarks>The name can be set within the Couchbase Management Console.</remarks>
        public string Name { get; set; }

        /// <summary>
        /// The password used to connect to the bucket.
        /// </summary>
        /// <remarks>The password can be set within the Couchbase Management Console.</remarks>
        public string Password { get; set; }

        /// <summary>
        /// The connection pool settings, which override the settings in <see cref="CouchbaseClientDefinition.ConnectionPool"/>.
        /// </summary>
        /// <remarks>The default settings are: MinSize=1, MaxSize=2, WaitTimout=2500ms, ShutdownTimeout=10000ms.</remarks>
        public ConnectionPoolDefinition ConnectionPool { get; set; }

        /// <summary>
        /// The max time an observe operation will take before timing out.
        /// </summary>
        public int ObserveInterval { get; set; }

        /// <summary>
        /// The interval between each observe attempt.
        /// </summary>
        public int ObserveTimeout { get; set; }

        /// <summary>
        /// The operation lifespan, maximum time in milliseconds allowed for an operation to run.
        /// </summary>
        public uint? OperationLifespan { get; set; }

        public BucketDefinition()
        {
            UseEnhancedDurability = PoolConfiguration.Defaults.UseEnhancedDurability;
            Name = BucketConfiguration.Defaults.BucketName;
            Password =  BucketConfiguration.Defaults.Password;
            ObserveInterval = BucketConfiguration.Defaults.ObserveInternal;
            ObserveTimeout = BucketConfiguration.Defaults.ObserverTimeout;
        }

        #region Additional ICouchbaseClientDefinition Implementations

        IConnectionPoolDefinition IBucketDefinition.ConnectionPool
        {
            get { return ConnectionPool; }
        }

        #endregion
    }
}
