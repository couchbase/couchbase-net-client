using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// POCO configuration definition which can be used to deserialize a configuration file using JSON, XML, or
    /// other configuration formats.  This can then be provided as a parameter to construct a <see cref="ClientConfiguration"/>
    /// object.
    /// </summary>
    public class CouchbaseClientDefinition : ICouchbaseClientDefinition
    {
        /// <summary>
        /// If true, use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>Set to true to require all buckets to use SSL.</remarks>
        /// <remarks>Set to false and then set UseSSL at the individual Bucket level to use SSL on specific buckets.</remarks>
        public bool UseSsl { get; set; }

        /// <summary>
        /// The Couchbase Server's list of bootstrap URI's. The client will use the list to connect to initially connect to the cluster.
        /// If null, then localhost will be used.
        /// </summary>
        public List<Uri> Servers { get; set; }

        /// <summary>
        /// Allows specific configurations of Bucket's to be defined, overriding the parent's settings.
        /// </summary>
        public List<BucketDefinition> Buckets { get; set;  }

        /// <summary>
        /// Overrides the default and sets the SSL port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 11207.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom SSL port.</remarks>
        public int SslPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Views REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Views REST API port.</remarks>
        public int ApiPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom port.
        /// </summary>
        /// <remarks>The default and suggested port for the Views REST API is 8091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Management REST API port.</remarks>
        public int MgmtPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the direct port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        /// <remarks>The default and suggested direct port is 11210.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom direct port.</remarks>
        public int DirectPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Couchbase Management REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18091.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Management REST API SSL port.</remarks>
        public int HttpsMgmtPort { get; set; }

        /// <summary>
        /// Overrides the default and sets the Couchbase Views REST API to use a custom SSL port.
        /// </summary>
        /// <remarks>The default and suggested port for SSL is 18092.</remarks>
        /// <remarks>Only set if you wish to override the default behavior.</remarks>
        /// <remarks>Requires UseSSL to be true.</remarks>
        /// <remarks>The Couchbase Server/Cluster needs to be configured to use a custom Couchbase Views REST API SSL port.</remarks>
        public int HttpsApiPort { get; set; }

        /// <summary>
        /// The max time an observe operation will take before timing out.
        /// </summary>
        public int ObserveInterval { get; set; }

        /// <summary>
        /// The interval between each observe attempt.
        /// </summary>
        public int ObserveTimeout { get; set; }

        /// <summary>
        /// The maximum number of times the client will retry a View operation if it has failed for a retriable reason.
        /// </summary>
        public int MaxViewRetries { get; set; }

        /// <summary>
        /// The maximum number of times the client will retry a View operation if it has failed for a retriable reason.
        /// </summary>
        public int ViewHardTimeout { get; set; }

        /// <summary>
        /// The interval for configuration "heartbeat" checks, which check for changes in the configuration that are otherwise undetected by the client.
        /// </summary>
        /// <remarks>The default is 10000ms.</remarks>
        public int HeartbeatConfigInterval { get; set; }

        /// <summary>
        /// Enables configuration "heartbeat" checks.
        /// </summary>
        /// <remarks>The default is "enabled" or true.</remarks>
        /// <remarks>The interval of the configuration hearbeat check is controlled by the <see cref="HeartbeatConfigInterval"/> property.</remarks>
        public bool EnableConfigHeartBeat { get; set; }

        /// <summary>
        /// The timeout for each HTTP View request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        public int ViewRequestTimeout { get; set; }

        /// <summary>
        /// The timeout for each HTTP N1QL query request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        public uint QueryRequestTimeout { get; set; }

        /// <summary>
        /// The timeout for each FTS request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        public uint SearchRequestTimeout { get; set; }

        /// <summary>
        /// A Boolean value that determines whether 100-Continue behavior is used.
        /// </summary>
        /// <remarks>The default is false.</remarks>
        public bool Expect100Continue { get; set; }

        /// <summary>
        /// The maximum number of concurrent connections allowed by a ServicePoint object used for making View and N1QL requests.
        /// </summary>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.defaultconnectionlimit.aspx</remarks>
        /// <remarks>The default is set to 5 connections.</remarks>
        public int DefaultConnectionLimit { get; set; }

        /// <summary>
        /// The maximum idle time of a ServicePoint object used for making View and N1QL requests.
        /// </summary>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.maxservicepointidletime.aspx</remarks>
        public int MaxServicePointIdleTime { get; set; }

        /// <summary>
        /// If true, writes the elasped time for an operation to the log appender. Disabled by default.
        /// </summary>
        /// <remarks>When enabled will cause severe performance degradation.</remarks>
        /// <remarks>Requires a <see cref="LogLevel"/> of DEBUG to be enabled as well.</remarks>
        public bool EnableOperationTiming { get; set; }

        /// <summary>
        /// An uint value that determines the maximum lifespan of an operation before it is abandonned.
        /// </summary>
        /// <remarks>The default is 2500 (2.5 seconds).</remarks>
        public uint OperationLifespan { get; set; }

        /// <summary>
        /// If true, indicates to enable TCP keep alives.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable TCP keep alives; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>The default is true; TCP Keep Alives are enabled.</remarks>
        public bool EnableTcpKeepAlives { get; set; }

        /// <summary>
        /// Specifies the timeout, in milliseconds, with no activity until the first keep-alive packet is sent.
        /// </summary>
        /// <value>
        /// The TCP keep alive time in milliseconds.
        /// </value>
        /// <remarks>The default is 2hrs.</remarks>
        public uint TcpKeepAliveTime { get; set; }

        /// <summary>
        /// Specifies the interval, in milliseconds, between when successive keep-alive packets are sent if no acknowledgement is received.
        /// </summary>
        /// <value>
        /// The TCP keep alive interval in milliseconds..
        /// </value>
        /// <remarks>The default is 1 second.</remarks>
        public uint TcpKeepAliveInterval { get; set; }

        /// <summary>
        /// The fully qualified type name of the transcoder.  If null the default transcoder is used.
        /// </summary>
        /// <value>
        /// The transcoder.
        /// </value>
        /// <remarks>The transcoder must implement <see cref="ITypeTranscoder"/>.</remarks>
        public string Transcoder { get; set; }

        /// <summary>
        /// The fully qualified type name of the converter.  If null the default convert is used.
        /// </summary>
        /// <value>
        /// The converter.
        /// </value>
        /// <remarks>The converter must implement <see cref="IByteConverter"/>.</remarks>
        public string Converter { get; set; }

        /// <summary>
        /// The fully qualified type name of the serializer.  If null the default serializer is used.
        /// </summary>
        /// <value>
        /// The serializer.
        /// </value>
        /// <remarks>The serializer must implement <see cref="ITypeSerializer"/> or <see cref="IExtendedTypeSerializer"/>.</remarks>
        public string Serializer { get; set; }

        /// <summary>
        /// The fully qualified type name of the transporter for IO.  If null the default IO service is used.
        /// </summary>
        /// <value>
        /// The transporter.
        /// </value>
        /// <remarks>The IO service must implement <see cref="IIOService"/></remarks>
        [JsonProperty("ioService")]
        public string IOService { get; set; }

        /// <summary>
        /// If the client detects that a node has gone offline it will check for connectivity at this interval.
        /// </summary>
        /// <remarks>The default is 1000ms.</remarks>
        /// <value>
        /// The node available check interval.
        /// </value>
        public uint NodeAvailableCheckInterval { get; set; }

        /// <summary>
        /// The default connection pool settings.  If null then defaults are used.
        /// </summary>
        /// <value>
        /// The default connection pool settings.
        /// </value>
        public ConnectionPoolDefinition ConnectionPool { get; set; }

        /// <summary>
        /// The count of IO errors within a specific interval defined by the value of <see cref="IOErrorCheckInterval" />.
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
        [JsonProperty("ioErrorThreshold")]
        public uint IOErrorThreshold { get; set; }

        /// <summary>
        /// The interval that the <see cref="IOErrorThreshold"/> will be checked. If the threshold is reached within the interval for a
        /// particular node, all keys mapped to that node the SDK will fail with a <see cref="NodeUnavailableException" /> in the
        /// <see cref="IOperationResult.Exception"/> field. The node will be flagged as "dead" and will try to reconnect, if connectivity
        /// is reached, the node will continue to process requests.
        /// </summary>
        /// <value>
        /// The io error check interval.
        /// </value>
        /// <remarks>The purpose of this is to distinguish between a remote host being unreachable or temporay network glitch.</remarks>
        /// <remarks>The default is 500ms; use milliseconds to override this: 1000 = 1 second.</remarks>
        [JsonProperty("ioErrorCheckInterval")]
        public uint IOErrorCheckInterval { get; set; }

        /// <summary>
        /// The query failed threshold for a <see cref="Uri"/> before it is flagged as "un-responsive".
        /// Once flagged as "un-responsive", no requests will be sent to that node until a server re-config has occurred
        /// and the <see cref="Uri"/> is added back into the pool. This is so the client will not send requests to
        /// a server node which is unresponsive.
        /// </summary>
        /// <remarks>The default is 2.</remarks>
        /// <value>
        /// The query failed threshold.
        /// </value>
        public int QueryFailedThreshold { get; set; }

        /// <summary>
        /// If TLS/SSL is enabled via <see cref="UseSsl"/> setting  this to <c>true</c> will disable hostname validation when authenticating
        /// connections to Couchbase Server. This is typically done in test or development enviroments where a domain name (FQDN) has not been
        /// specified for the bootstrap uri's <see cref="Servers"/> and the IP address is used to validate the certificate, which will fail with
        /// a RemoteCertificateNameMismatch error.
        /// </summary>
        /// <value>
        /// <c>true</c> to ignore hostname validation of the certificate if you are using IP's and not a FQDN to bootstrap; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>Note: this is a global setting - it applies to all <see cref="ICluster"/> and <see cref="IBucket"/> references within a process.</remarks>
        [JsonProperty("ignoreRemoteCertificateNameMismatch")]
        public bool IgnoreRemoteCertificateNameMismatch { get; set; }

        /// Gets or sets a value indicating whether use IP version 6 addresses.
        /// </summary>
        /// <value>
        /// <c>true</c> if <c>true</c> IP version 6 addresses will be used; otherwise, <c>false</c>.
        /// </value>
        public bool UseInterNetworkV6Addresses { get; set; }


        public CouchbaseClientDefinition()
        {
            UseSsl = ClientConfiguration.Defaults.UseSsl;
            SslPort = (int)ClientConfiguration.Defaults.SslPort;
            ApiPort = (int)ClientConfiguration.Defaults.ApiPort;
            MgmtPort = (int)ClientConfiguration.Defaults.MgmtPort;
            DirectPort = (int)ClientConfiguration.Defaults.DirectPort;
            HttpsMgmtPort = (int)ClientConfiguration.Defaults.HttpsMgmtPort;
            HttpsApiPort = (int)ClientConfiguration.Defaults.HttpsApiPort;
            ObserveInterval = (int)ClientConfiguration.Defaults.ObserveInterval;
            ObserveTimeout = (int)ClientConfiguration.Defaults.ObserveTimeout;
            MaxViewRetries = (int)ClientConfiguration.Defaults.MaxViewRetries;
            ViewHardTimeout = (int)ClientConfiguration.Defaults.ViewHardTimeout;
            HeartbeatConfigInterval = (int)ClientConfiguration.Defaults.HeartbeatConfigInterval;
            EnableConfigHeartBeat = ClientConfiguration.Defaults.EnableConfigHeartBeat;
            ViewRequestTimeout = (int)ClientConfiguration.Defaults.ViewRequestTimeout;
            QueryRequestTimeout = ClientConfiguration.Defaults.QueryRequestTimeout;
            SearchRequestTimeout = ClientConfiguration.Defaults.SearchRequestTimeout;
            Expect100Continue = ClientConfiguration.Defaults.Expect100Continue;
            DefaultConnectionLimit = ClientConfiguration.Defaults.DefaultConnectionLimit;
            MaxServicePointIdleTime = (int)ClientConfiguration.Defaults.MaxServicePointIdleTime;
            EnableOperationTiming = ClientConfiguration.Defaults.EnableOperationTiming;
            OperationLifespan = ClientConfiguration.Defaults.DefaultOperationLifespan;
            EnableTcpKeepAlives = ClientConfiguration.Defaults.EnableTcpKeepAlives;
            TcpKeepAliveTime = ClientConfiguration.Defaults.TcpKeepAliveTime;
            TcpKeepAliveInterval = ClientConfiguration.Defaults.TcpKeepAliveInterval;
            NodeAvailableCheckInterval = ClientConfiguration.Defaults.NodeAvailableCheckInterval;
            IOErrorThreshold = ClientConfiguration.Defaults.IOErrorThreshold;
            IOErrorCheckInterval = ClientConfiguration.Defaults.IOErrorCheckInterval;
            QueryFailedThreshold = (int)ClientConfiguration.Defaults.QueryFailedThreshold;
        }

        #region Additional ICouchbaseClientDefinition Implementations

        IEnumerable<Uri> ICouchbaseClientDefinition.Servers
        {
            get { return Servers; }
        }

        IEnumerable<IBucketDefinition> ICouchbaseClientDefinition.Buckets
        {
            get { return Buckets; }
        }

        IConnectionPoolDefinition ICouchbaseClientDefinition.ConnectionPool
        {
            get { return ConnectionPool; }
        }

        #endregion
    }
}
