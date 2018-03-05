#if NET45
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using Couchbase.Authentication;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Core;

namespace Couchbase.Configuration.Client.Providers
{
    /// <summary>
    /// Allows the Client Configuration to be set through an App.config or a Web.config.
    /// </summary>
    public sealed class CouchbaseClientSection : ConfigurationSection, ICouchbaseClientDefinition
    {
        /// <summary>
        /// A "couchbase://" or "couchbases://" connection string for the cluster.
        /// </summary>
        /// <remarks>
        /// Overrides settings for <see cref="Servers"/>, <see cref="UseSsl"/>, <see cref="SslPort"/>,
        /// <see cref="DirectPort"/>, and <see cref="ConfigurationProviders"/>.
        /// </remarks>
        [ConfigurationProperty("connectionString", DefaultValue = "", IsRequired = false)]
        public string ConnectionString{
            get { return (string) this["connectionString"]; }
            set { this["connectionString"] = value; }
        }

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
        /// Application user username to authenticate to the Couchbase cluster.
        /// </summary>
        /// <remarks>Internally creates a <see cref="PasswordAuthenticator"/> to authenticate with.</remarks>
        [ConfigurationProperty("username", DefaultValue = null, IsRequired = false)]
        public string Username
        {
            get { return (string) this["username"]; }
            set { this["username"] = value; }
        }

        /// <summary>
        /// Application user password to authenticate to the Couchbase Cluster.
        /// </summary>
        /// <remarks>Internally creates a <see cref="PasswordAuthenticator"/> to authenticate with.</remarks>
        [ConfigurationProperty("password", DefaultValue = null, IsRequired = false)]
        public string Password
        {
            get { return (string)this["password"]; }
            set { this["password"] = value; }
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

        /// <summary>
        /// The maximum number of times the client will retry a View operation if it has failed for a retriable reason.
        /// </summary>
        [ConfigurationProperty("maxViewRetries", DefaultValue = 2, IsRequired = false)]
        public int MaxViewRetries
        {
            get { return (int)this["maxViewRetries"]; }
            set { this["maxViewRetries"] = value; }
        }


        /// <summary>
        /// The maximum number of times the client will retry a View operation if it has failed for a retriable reason.
        /// </summary>
        [ConfigurationProperty("viewHardTimeout", DefaultValue = 30000, IsRequired = false)]
        public int ViewHardTimeout
        {
            get { return (int)this["viewHardTimeout"]; }
            set { this["viewHardTimeout"] = value; }
        }

        /// <summary>
        /// Sets the interval for configuration "heartbeat" checks, which check for changes in the configuration that are otherwise undetected by the client.
        /// </summary>
        /// <remarks>The default is 2500ms.</remarks>
        [ConfigurationProperty("heartbeatConfigInterval", DefaultValue = 2500, IsRequired = false)]
        public int HeartbeatConfigInterval
        {
            get { return (int)this["heartbeatConfigInterval"]; }
            set { this["heartbeatConfigInterval"] = value; }
        }

        /// <summary>
        /// Sets the interval for configuration "heartbeat" checks, which check for changes in the configuration that are otherwise undetected by the client.
        /// </summary>
        /// <remarks>The default is 2500ms.</remarks>
        [ConfigurationProperty("pollConfigInterval", DefaultValue = 2500u, IsRequired = false)]
        public uint ConfigPollInterval
        {
            get => (uint)this["pollConfigInterval"];
            set => this["pollConfigInterval"] = value;
        }

        /// <inheritdoc />
        [ConfigurationProperty("pollConfigEnabled", DefaultValue = true, IsRequired = false)]
        public bool ConfigPollEnabled
        {
            get => (bool)this["pollConfigEnabled"];
            set => this["pollConfigEnabled"] = value;
        }

        /// <summary>
        /// Enables configuration "heartbeat" checks.
        /// </summary>
        /// <remarks>The default is "enabled" or true.</remarks>
        /// <remarks>The interval of the configuration hearbeat check is controlled by the <see cref="HeartbeatConfigInterval"/> property.</remarks>
        [ConfigurationProperty("enableConfigHeartBeat", DefaultValue = true, IsRequired = false)]
        public bool EnableConfigHeartBeat
        {
            get { return (bool)this["enableConfigHeartBeat"]; }
            set { this["enableConfigHeartBeat"] = value; }
        }

        /// <summary>
        /// Sets the timeout for each HTTP View request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        [ConfigurationProperty("viewRequestTimeout", DefaultValue = 75000, IsRequired = false)]
        public int ViewRequestTimeout
        {
            get { return (int)this["viewRequestTimeout"]; }
            set { this["viewRequestTimeout"] = value; }
        }

        /// <summary>
        /// Sets the timeout for each HTTP N1QL query request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        [ConfigurationProperty("queryRequestTimeout", DefaultValue = "75000", IsRequired = false)]
        public uint QueryRequestTimeout
        {
            get { return (uint)this["queryRequestTimeout"]; }
            set { this["queryRequestTimeout"] = value; }
        }

        /// <summary>
        /// Gets or sets whether the elasped client time, elasped cluster time and query statement for a N1QL query requst are written to the log appender.
        /// </summary>
        /// <remarks>When enabled will cause severe performance degradation.</remarks>
        /// <remarks>Requires a <see cref="LogLevel"/>of INFO to be enabled as well.</remarks>
        [ConfigurationProperty("enableQueryTiming", DefaultValue = false, IsRequired = false)]
        public bool EnableQueryTiming
        {
            get { return (bool) this["enableQueryTiming"]; }
            set { this["enableQueryTiming"] = value; }
        }

        /// <summary>
        /// Sets the timeout for each FTS request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        [ConfigurationProperty("searchRequestTimeout", DefaultValue = "75000", IsRequired = false)]
        public uint SearchRequestTimeout
        {
            get { return (uint)this["searchRequestTimeout"]; }
            set { this["searchRequestTimeout"] = value; }
        }

        /// <summary>
        /// Gets or sets a Boolean value that determines whether 100-Continue behavior is used.
        /// </summary>
        /// <remarks>The default is false.</remarks>
        [ConfigurationProperty("expect100Continue", DefaultValue = false, IsRequired = false)]
        public bool Expect100Continue
        {
            get { return (bool)this["expect100Continue"]; }
            set { this["expect100Continue"] = value; }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections allowed by a ServicePoint object used for making View and N1QL requests.
        /// </summary>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.defaultconnectionlimit.aspx</remarks>
        /// <remarks>The default is set to 5 connections.</remarks>
        [ConfigurationProperty("defaultConnectionLimit", DefaultValue = 5, IsRequired = false)]
        public int DefaultConnectionLimit
        {
            get { return (int)this["defaultConnectionLimit"]; }
            set { this["defaultConnectionLimit"] = value; }
        }

        /// <summary>
        /// Gets or sets the maximum idle time of a ServicePoint object used for making View and N1QL requests.
        /// </summary>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.maxservicepointidletime.aspx</remarks>
        [ConfigurationProperty("maxServicePointIdleTime", DefaultValue = 1000, IsRequired = false)]
        public int MaxServicePointIdleTime
        {
            get { return (int)this["maxServicePointIdleTime"]; }
            set { this["maxServicePointIdleTime"] = value; }
        }

        /// <summary>
        /// Writes the elasped time for an operation to the log appender. Disabled by default.
        /// </summary>
        /// <remarks>When enabled will cause severe performance degradation.</remarks>
        /// <remarks>Requires a <see cref="LogLevel"/>of DEBUG to be enabled as well.</remarks>
        [ConfigurationProperty("enableOperationTiming", DefaultValue = false, IsRequired = false)]
        public bool EnableOperationTiming
        {
            get { return (bool)this["enableOperationTiming"]; }
            set { this["enableOperationTiming"] = value; }
        }

        /// <summary>
        /// Gets or sets an uint value that determines the maximum lifespan of an operation before it is abandonned.
        /// </summary>
        /// <remarks>The default is 2500 (2.5 seconds).</remarks>
        [ConfigurationProperty("operationLifespan", DefaultValue = (uint) 2500, IsRequired = false)]
        public uint OperationLifespan
        {
            get { return (uint)this["operationLifespan"]; }
            set { this["operationLifespan"] = value; }
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
        [ConfigurationProperty("tcpKeepAliveInterval", DefaultValue = ((uint) 1000), IsRequired = false)]
        public uint TcpKeepAliveInterval
        {
            get { return (uint) this["tcpKeepAliveInterval"]; }
            set { this["tcpKeepAliveInterval"] = value; }
        }

        /// <summary>
        /// Gets or sets the transcoder.
        /// </summary>
        /// <value>
        /// The transcoder.
        /// </value>
        [ConfigurationProperty("transcoder", IsRequired = false)]
        public TranscoderElement Transcoder
        {
            get { return (TranscoderElement)this["transcoder"]; }
            set { this["transcoder"] = value; }
        }

        /// <summary>
        /// Gets or sets the converter.
        /// </summary>
        /// <value>
        /// The converter.
        /// </value>
        [ConfigurationProperty("converter", IsRequired = false)]
        public ConverterElement Converter
        {
            get { return (ConverterElement)this["converter"]; }
            set { this["converter"] = value; }
        }

        /// <summary>
        /// Gets or sets the serializer.
        /// </summary>
        /// <value>
        /// The serializer.
        /// </value>
        [ConfigurationProperty("serializer", IsRequired = false)]
        public SerializerElement Serializer
        {
            get { return (SerializerElement)this["serializer"]; }
            set { this["serializer"] = value; }
        }

        /// <summary>
        /// Gets or sets the transporter for IO.
        /// </summary>
        /// <value>
        /// The transporter.
        /// </value>
        [ConfigurationProperty("ioService", IsRequired = false)]
        public IOServiceElement IOService
        {
            get { return (IOServiceElement)this["ioService"]; }
            set { this["ioService"] = value; }
        }

        /// <summary>
        /// Indicates if the client should use connection pooling instead of Multiplexing operations over
        /// the fewer connections. Defaults to false.
        /// </summary>
        [ConfigurationProperty("useConnectionPooling", IsRequired = false, DefaultValue = false)]
        public bool UseConnectionPooling
        {
            get { return (bool)this["useConnectionPooling"]; }
            set { this["useConnectionPooling"] = value; }
        }

        /// <summary>
        /// Indicates if the client should monitor down services using ping requests and reactivate when they
        /// are back online.  Pings every <see cref="NodeAvailableCheckInterval"/>ms.  Defaults to true.
        /// </summary>
        [ConfigurationProperty("enableDeadServiceUriPing", IsRequired = false, DefaultValue = true)]
        public bool EnableDeadServiceUriPing
        {
            get { return (bool)this["enableDeadServiceUriPing"]; }
            set { this["enableDeadServiceUriPing"] = value; }
        }

        /// <summary>
        /// If the client detects that a node has gone offline it will check for connectivity at this interval.
        /// </summary>
        /// <remarks>The default is 1000ms.</remarks>
        /// <value>
        /// The node available check interval.
        /// </value>
        [ConfigurationProperty("nodeAvailableCheckInterval", DefaultValue = ((uint)1000), IsRequired = false)]
        public uint NodeAvailableCheckInterval
        {
            get { return (uint)this["nodeAvailableCheckInterval"]; }
            set { this["nodeAvailableCheckInterval"] = value; }
        }

        /// <summary>
        /// Gets or sets the default connection pool settings.
        /// </summary>
        /// <value>
        /// The default connection pool settings.
        /// </value>
        [ConfigurationProperty("connectionPool", IsRequired = false)]
        public ConnectionPoolElement ConnectionPool
        {
            get { return (ConnectionPoolElement)this["connectionPool"]; }
            set { this["connectionPool"] = value; }
        }

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
        [ConfigurationProperty("ioErrorThreshold", IsRequired = false, DefaultValue = 10u)]
        public uint IOErrorThreshold
        {
            get { return (uint)this["ioErrorThreshold"]; }
            set { this["ioErrorThreshold"] = value; }
        }

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
        [ConfigurationProperty("ioErrorCheckInterval", IsRequired = false, DefaultValue = 500u)]
        public uint IOErrorCheckInterval
        {
            get { return (uint)this["ioErrorCheckInterval"]; }
            set { this["ioErrorCheckInterval"] = value; }
        }

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
        [ConfigurationProperty("queryFailedThreshold", IsRequired = false, DefaultValue = 2)]
        public int QueryFailedThreshold
        {
            get { return (int)this["queryFailedThreshold"]; }
            set { this["queryFailedThreshold"] = value; }
        }

        /// <summary>
        /// If TLS/SSL is enabled via <see cref="UseSsl"/> setting  this to <c>true</c> will disable hostname validation when authenticating
        /// connections to Couchbase Server. This is typically done in test or development enviroments where a domain name (FQDN) has not been
        /// specified for the bootstrap uri's <see cref="Servers"/> and the IP address is used to validate the certificate, which will fail with
        /// a RemoteCertificateNameMismatch error.
        /// </summary>
        /// <value>
        /// <c>true</c> to ignore hostname validation of the certificate if you are using IP's and not a FQDN to bootstrap; otherwise, <c>false</c>.
        /// </value>
        [ConfigurationProperty("ignoreRemoteCertificateNameMismatch", IsRequired = false, DefaultValue = false)]
        public bool IgnoreRemoteCertificateNameMismatch
        {
            get { return (bool) this["ignoreRemoteCertificateNameMismatch"]; }
            set { this["ignoreRemoteCertificateNameMismatch"] = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether use IP version 6 addresses.
        /// </summary>
        /// <value>
        /// <c>true</c> if <c>true</c> IP version 6 addresses will be used; otherwise, <c>false</c>.
        /// </value>
        [ConfigurationProperty("useInterNetworkV6Addresses", IsRequired = false, DefaultValue = false)]
        public bool UseInterNetworkV6Addresses
        {
            get { return (bool) this["useInterNetworkV6Addresses"]; }
            set { this["useInterNetworkV6Addresses"] = value; }
        }

        /// <summary>
        /// Gets or sets the VBucket retry sleep time: the default is 100ms.
        /// </summary>
        /// <value>
        /// The VBucket retry sleep time.
        /// </value>
        [ConfigurationProperty("vBucketRetrySleepTime", IsRequired = false, DefaultValue = 100u)]
        public uint VBucketRetrySleepTime
        {
            get { return (uint)this["vBucketRetrySleepTime"]; }
            set { this["vBucketRetrySleepTime"] = value; }

        }

        /// <summary>
        /// Gets or sets the server resolver configuration.
        /// </summary>
        [ConfigurationProperty("serverResolver", IsRequired = false)]
        public ServerResolverElement ServerResolver
        {
            get { return (ServerResolverElement)this["serverResolver"]; }
            set { this["serverResolver"] = value; }
        }

        /// <summary>
        /// Gets or sets the heartbeat configuration check floor - which is the minimum time between config checks.
        /// </summary>
        /// <value>
        /// The heartbeat configuration check floor.
        /// </value>
        /// <remarks>The default is 50ms.</remarks>
        [ConfigurationProperty("heartbeatConfigCheckFloor", DefaultValue = 50u, IsRequired = false)]
        [Obsolete("Use ConfigPollCheckFloor.")]
        public uint HeartbeatConfigCheckFloor
        {
            get { return (uint)this["heartbeatConfigCheckFloor"]; }
            set { this["heartbeatConfigCheckFloor"] = value; }
        }


        /// <summary>
        /// Gets or sets the heartbeat configuration check floor - which is the minimum time between config checks.
        /// </summary>
        /// <value>
        /// The heartbeat configuration check floor.
        /// </value>
        /// <remarks>The default is 50ms.</remarks>
        [ConfigurationProperty("pollConfigCheckFloor", DefaultValue = 50u, IsRequired = false)]
        public uint ConfigPollCheckFloor
        {
            get => (uint)this["pollConfigCheckFloor"];
            set => this["pollConfigCheckFloor"] = value;
        }

        /// <inheritdoc />
        [ConfigurationProperty("providers",
            DefaultValue = ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming,
            IsRequired = false)]
        public ServerConfigurationProviders ConfigurationProviders {
            get => (ServerConfigurationProviders)this["providers"];
            set => this["providers"] = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the client must use the Plain SASL mechanism to authenticate KV connections.
        /// </summary>
        /// <value>
        /// <c>true</c> if the client must use Plain SASL authentication; otherwise, <c>false</c>.
        /// </value>
        [ConfigurationProperty("forceSaslPlain", IsRequired = false, DefaultValue = false)]
        public bool ForceSaslPlain
        {
            get => (bool) this["forceSaslPlain"];
            set => this["forceSaslPlain"] = value;
        }

        /// <summary>
        /// Controls whether the <see cref="T:Couchbase.Tracing.ThresholdLoggingTracer" /> is used when configuring the client.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="T:Couchbase.Tracing.ThresholdLoggingTracer" /> is to be used; otherwise, <c>false</c>.
        /// </value>
        [ConfigurationProperty("operationTracingEnabled", IsRequired = false, DefaultValue = false)]
        public bool OperationTracingEnabled
        {
            get => (bool)this["operationTracingEnabled"];
            set => this["operationTracingEnabled"] = value;
        }

        /// <summary>
        /// Controls whether orphaned server responses are recorded and logged.
        /// </summary>
        /// <value>
        /// <c>true</c> if orphaned server responses are logged; otherwise, <c>false</c>.
        /// </value>
        [ConfigurationProperty("orphanedResponseLoggingEnabled", IsRequired = false, DefaultValue = false)]
        public bool OrphanedResponseLoggingEnabled
        {
            get => (bool)this["orphanedResponseLoggingEnabled"];
            set => this["orphanedResponseLoggingEnabled"] = value;
        }

        #region Additional ICouchbaseClientDefinition implementations

        IEnumerable<Uri> ICouchbaseClientDefinition.Servers
        {
            get { return Servers.Cast<UriElement>().Select(p => p.Uri); }
        }

        IEnumerable<IBucketDefinition> ICouchbaseClientDefinition.Buckets
        {
            get { return Buckets.Cast<IBucketDefinition>(); }
        }

        IConnectionPoolDefinition ICouchbaseClientDefinition.ConnectionPool
        {
            get { return ConnectionPool.ElementInformation.IsPresent ? ConnectionPool : null; }
        }

        string ICouchbaseClientDefinition.Transcoder
        {
            get { return Transcoder.Type; }
        }

        string ICouchbaseClientDefinition.Converter
        {
            get { return Converter.Type; }
        }

        string ICouchbaseClientDefinition.Serializer
        {
            get { return Serializer.Type; }
        }

        string ICouchbaseClientDefinition.IOService
        {
            get { return IOService.Type; }
        }

        string ICouchbaseClientDefinition.ServerResolverType
        {
            get { return ServerResolver != null ? ServerResolver.Type : null; }
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
