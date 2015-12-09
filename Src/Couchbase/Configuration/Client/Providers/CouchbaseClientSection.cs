using System.Collections.Generic;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Allows the Client Configuration to be set through an App.config or a Web.config.
    /// </summary>
    public sealed class CouchbaseClientSection
    {
        /// <summary>
        /// Set to true to use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>Set to true to require all buckets to use SSL.</remarks>
        /// <remarks>Set to false and then set UseSSL at the individual Bucket level to use SSL on specific buckets.</remarks>
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Sets the Couchbase Server's list of bootstrap URI's. The client will use the list to connect to initially connect to the cluster.
        /// </summary>
        public IEnumerable<UriElement> Servers { get; set; } = new List<UriElement>();

        /// <summary>
        /// Allows specific configurations of Bucket's to be defined, overriding the parent's settings.
        /// </summary>
        public IEnumerable<BucketElement> Buckets { get; set; } = new List<BucketElement>();

        /// <summary>
        /// Overrides the default and sets the SSL port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 11207.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom SSL port.</remarks>
        public int SslPort { get; set; } = 11207;

        /// <summary>
        /// Overrides the default and sets the Views REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Views REST API port.</remarks>
        public int ApiPort { get; set; } = 8092;

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Management REST API port.</remarks>
        public int MgmtPort { get; set; } = 8091;

        /// <summary>
        /// Overrides the default and sets the direct port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        /// <remarks>The default and suggested direct port is 11210.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom direct port.</remarks>
        public int DirectPort { get; set; } = 11210;

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Management REST API SSL port.</remarks>
        public int HttpsMgmtPort { get; set; } = 18091;

        /// <summary>
        /// Overrides the default and sets the Couchbase Views REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Views REST API SSL port.</remarks>
        public int HttpsApiPort { get; set; } = 18092;

        /// <summary>
        /// Gets or Sets the max time an observe operation will take before timing out.
        /// </summary>
        public int ObserveInterval { get; set; } = 2;

        /// <summary>
        /// Gets or Sets the interval between each observe attempt.
        /// </summary>
        public int ObserveTimeout { get; set; } = 500;

        /// <summary>
        /// The maximum number of times the client will retry a View operation if it has failed for a retriable reason.
        /// </summary>
        public int MaxViewRetries { get; set; } = 2;


        /// <summary>
        /// The maximum number of times the client will retry a View operation if it has failed for a retriable reason.
        /// </summary>
        public int ViewHardTimeout { get; set; } = 30000;

        /// <summary>
        /// Sets the interval for configuration "heartbeat" checks, which check for changes in the configuration that are otherwise undetected by the client.
        /// </summary>
        /// <remarks>The default is 10000ms.</remarks>
        public int HeartbeatConfigInterval { get; set; } = 10000;

        /// <summary>
        /// Enables configuration "heartbeat" checks.
        /// </summary>
        /// <remarks>The default is "enabled" or true.</remarks>
        /// <remarks>The interval of the configuration hearbeat check is controlled by the <see cref="HeartbeatConfigInterval"/> property.</remarks>
        public bool EnableConfigHeartBeat { get; set; } = true;

        /// <summary>
        /// Sets the timeout for each HTTP View request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        public int ViewRequestTimeout { get; set; } = 75000;

        /// <summary>
        /// Sets the timeout for each HTTP N1QL query request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        public uint QueryRequestTimeout { get; set; } = 75000;

        /// <summary>
        /// Gets or sets a Boolean value that determines whether 100-Continue behavior is used.
        /// </summary>
        /// <remarks>The default is false.</remarks>
        public bool Expect100Continue { get; set; } = false;

        /// <summary>
        /// Writes the elasped time for an operation to the log appender. Disabled by default.
        /// </summary>
        /// <remarks>When enabled will cause severe performance degradation.</remarks>
        /// <remarks>Requires a <see cref="LogLevel"/>of DEBUG to be enabled as well.</remarks>
        public bool EnableOperationTiming { get; set; } = false;

        /// <summary>
        /// Gets or sets an uint value that determines the maximum lifespan of an operation before it is abandonned.
        /// </summary>
        /// <remarks>The default is 2500 (2.5 seconds).</remarks>
        public uint OperationLifespan { get; set; } = 2500;

        /// <summary>
        /// Gets or sets a value indicating whether enable TCP keep alives.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable TCP keep alives; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>The default is true; TCP Keep Alives are enabled.</remarks>
        public bool EnableTcpKeepAlives { get; set; } = true;

        /// <summary>
        /// Specifies the timeout, in milliseconds, with no activity until the first keep-alive packet is sent.
        /// </summary>
        /// <value>
        /// The TCP keep alive time in milliseconds.
        /// </value>
        /// <remarks>The default is 2hrs.</remarks>
        public uint TcpKeepAliveTime { get; set; } = 2 * 60 * 60 * 1000;

        /// <summary>
        /// Specifies the interval, in milliseconds, between when successive keep-alive packets are sent if no acknowledgement is received.
        /// </summary>
        /// <value>
        /// The TCP keep alive interval in milliseconds..
        /// </value>
        /// <remarks>The default is 1 second.</remarks>
        public uint TcpKeepAliveInterval { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the transcoder.
        /// </summary>
        /// <value>
        /// The transcoder.
        /// </value>
        public TranscoderElement Transcoder { get; set; } = null;

        /// <summary>
        /// Gets or sets the converter.
        /// </summary>
        /// <value>
        /// The converter.
        /// </value>
        public ConverterElement Converter { get; set; } = null;

        /// <summary>
        /// Gets or sets the serializer.
        /// </summary>
        /// <value>
        /// The serializer.
        /// </value>
        public SerializerElement Serializer { get; set; } = null;

        /// <summary>
        /// If the client detects that a node has gone offline it will check for connectivity at this interval.
        /// </summary>
        /// <remarks>The default is 1000ms.</remarks>
        /// <value>
        /// The node available check interval.
        /// </value>
        public uint NodeAvailableCheckInterval { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the default connection pool settings.
        /// </summary>
        /// <value>
        /// The default connection pool settings.
        /// </value>
        public ConnectionPoolElement ConnectionPool { get; set; } = null;

        /// <summary>
        /// Gets or sets the count of IO errors within a specific interval defined by the value of <see cref="IOErrorCheckInterval" />.
        /// If the threshold is reached within the interval for a particular node, all keys mapped to that node the SDK will fail
        /// with a <see cref="NodeUnavailableException" /> in the <see cref="IOperationResult.Exception"/> field. The node will be flagged as "dead"
        /// and will try to reconnect, if connectivity is reached, the node will continue to process requests.
        /// </summary>
        /// <value>
        /// The io error count threshold.
        /// </value>
        /// <remarks>
        /// The purpose of this is to distinguish between a remote host being unreachable or temporay network glitch.
        /// </remarks>
        /// <remarks>The default is 10 errors.</remarks>
        /// <remarks>The lower limit is 0; the default will apply if this is exceeded.</remarks>
        public uint IOErrorThreshold { get; set; } = 10u;

        /// <summary>
        /// Gets or sets the interval that the <see cref="IOErrorThreshold"/> will be checked. If the threshold is reached within the interval for a
        /// particular node, all keys mapped to that node the SDK will fail with a <see cref="NodeUnavailableException" /> in the
        /// <see cref="IOperationResult.Exception"/> field. The node will be flagged as "dead" and will try to reconnect, if connectivity
        /// is reached, the node will continue to process requests.
        /// </summary>
        /// <value>
        /// The io error check interval.
        /// </value>
        /// <remarks>The purpose of this is to distinguish between a remote host being unreachable or temporay network glitch.</remarks>
        /// <remarks>The default is 500ms; use milliseconds to override this: 1000 = 1 second.</remarks>
        public uint IOErrorCheckInterval { get; set; } = 500u;

        /// <summary>
        /// Gets or sets the query failed threshold for a <see cref="Uri"/> before it is flagged as "un-responsive".
        /// Once flagged as "un-responsive", no requests will be sent to that node until a server re-config has occurred
        /// and the <see cref="Uri"/> is added back into the pool. This is so the client will not send requests to
        /// a server node which is unresponsive.
        /// </summary>
        /// <remarks>The default is 2.</remarks>
        /// <value>
        /// The query failed threshold.
        /// </value>
        public int QueryFailedThreshold { get; set; } = 2;
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