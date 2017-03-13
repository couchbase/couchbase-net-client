using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Couchbase.Logging;
using Couchbase.Authentication;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

#if NET45
using Couchbase.Configuration.Client.Providers;
#endif
#if NETSTANDARD
using Microsoft.Extensions.Logging;
#endif

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// Represents the configuration of a <see cref="Cluster"/> object. The <see cref="Cluster"/> object
    /// will use this class to construct it's internals.
    /// </summary>
    public class ClientConfiguration
    {
        protected ReaderWriterLockSlim ConfigLock = new ReaderWriterLockSlim();
        private const string DefaultBucket = "default";
        private PoolConfiguration _poolConfiguration;
        private bool _poolConfigurationChanged;
        private List<Uri> _servers = new List<Uri>();
        private bool _serversChanged;
        private bool _useSsl;
        private bool _useSslChanged;
        private int _maxViewRetries;
        private int _viewHardTimeout;
        private double _heartbeatConfigInterval;
        private int _viewRequestTimeout;
        private uint _operationLifespan;
        private bool _operationLifespanChanged;

        public static class Defaults
        {
            public static Uri Server = new Uri("http://localhost:8091/pools");
            public static uint QueryRequestTimeout = 75000;
            public static bool EnableQueryTiming = false;
            public static bool UseSsl = false;
            public static uint SslPort = 11207;
            public static uint ApiPort = 8092;
            public static uint DirectPort = 11210;
            public static uint MgmtPort = 8091;
            public static uint HttpsMgmtPort = 18091;
            public static uint HttpsApiPort = 18092;
            public static uint ObserveInterval = 10; //ms
            public static uint ObserveTimeout = 500; //ms
            public static uint MaxViewRetries = 2;
            public static uint ViewHardTimeout = 30000; //ms
            public static uint HeartbeatConfigInterval = 10000; //ms
            public static bool EnableConfigHeartBeat = true;
            public static uint ViewRequestTimeout = 75000; //ms
            public static uint SearchRequestTimeout = 75000; //ms
            public static uint VBucketRetrySleepTime = 100; //ms

            //service point settings
            public static int DefaultConnectionLimit = 5; //connections
            public static bool Expect100Continue = false;
            public static uint MaxServicePointIdleTime = 100;

            public static bool EnableOperationTiming = false;
            public static uint BufferSize = 1024 * 16;
            public static uint DefaultOperationLifespan = 2500;//ms
            public static uint QueryFailedThreshold = 2;

            //keep alive settings
            public static bool EnableTcpKeepAlives = true;
            public static uint TcpKeepAliveTime = 2*60*60*1000;
            public static uint TcpKeepAliveInterval = 1000;

            public static uint NodeAvailableCheckInterval = 1000;//ms
            public static uint IOErrorCheckInterval = 500;
            public static uint IOErrorThreshold = 10;

            public static bool UseConnectionPooling = false;
            public static bool EnableDeadServiceUriPing = true;
        }

        public ClientConfiguration()
        {
            //For operation timing
            Timer = TimingFactory.GetTimer();
            QueryRequestTimeout = Defaults.QueryRequestTimeout;
            EnableQueryTiming = Defaults.EnableQueryTiming;
            UseSsl = Defaults.UseSsl;
            SslPort = (int) Defaults.SslPort;
            ApiPort = (int) Defaults.ApiPort;
            DirectPort = (int) Defaults.DirectPort;
            MgmtPort = (int) Defaults.MgmtPort;
            HttpsMgmtPort = (int) Defaults.HttpsMgmtPort;
            HttpsApiPort = (int) Defaults.HttpsApiPort;
            ObserveInterval = (int) Defaults.ObserveInterval; //ms
            ObserveTimeout = (int) Defaults.ObserveTimeout; //ms
            MaxViewRetries = (int) Defaults.MaxViewRetries;
#pragma warning disable 618
            ViewHardTimeout = (int) Defaults.ViewHardTimeout; //ms
#pragma warning restore 618
            HeartbeatConfigInterval = Defaults.HeartbeatConfigInterval; //ms
            EnableConfigHeartBeat = Defaults.EnableConfigHeartBeat;
#pragma warning disable 618
            SerializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            DeserializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
#pragma warning restore 618
            ViewRequestTimeout = (int) Defaults.ViewRequestTimeout; //ms
            SearchRequestTimeout = Defaults.SearchRequestTimeout;
            VBucketRetrySleepTime = Defaults.VBucketRetrySleepTime;

            //service point settings
            DefaultConnectionLimit = Defaults.DefaultConnectionLimit; //connections
            Expect100Continue = Defaults.Expect100Continue;
            MaxServicePointIdleTime = (int) Defaults.MaxServicePointIdleTime;

            EnableOperationTiming = Defaults.EnableOperationTiming;
            BufferSize = (int) Defaults.BufferSize;
            DefaultOperationLifespan = Defaults.DefaultOperationLifespan;//ms
            EnableTcpKeepAlives = Defaults.EnableTcpKeepAlives;
            QueryFailedThreshold = (int) Defaults.QueryFailedThreshold;

            TcpKeepAliveTime = Defaults.TcpKeepAliveTime;
            TcpKeepAliveInterval = Defaults.TcpKeepAliveInterval;

            NodeAvailableCheckInterval = Defaults.NodeAvailableCheckInterval;//ms
            IOErrorCheckInterval = Defaults.IOErrorCheckInterval;
            IOErrorThreshold = Defaults.IOErrorThreshold;
            EnableDeadServiceUriPing = Defaults.EnableDeadServiceUriPing;

            //the default serializer
            Serializer = SerializerFactory.GetSerializer();

            //the default byte converter
            Converter = ConverterFactory.GetConverter();

            //the default transcoder
            Transcoder = TranscoderFactory.GetTranscoder(this);

            //the default ioservice
            IOServiceCreator = IOServiceFactory.GetFactory(this);

            //the default connection pool creator
            ConnectionPoolCreator = ConnectionPoolFactory.GetFactory();

            //The default sasl mechanism creator
            CreateSaslMechanism = SaslFactory.GetFactory();

#if NETSTANDARD
            LoggerFactory = new LoggerFactory();
#endif

            PoolConfiguration = new PoolConfiguration(this)
            {
                BufferSize = BufferSize,
                BufferAllocator = (p) => new BufferAllocator(p.MaxSize * p.BufferSize, p.BufferSize)
            };

            BucketConfigs = new Dictionary<string, BucketConfiguration>
            {
                {DefaultBucket, new BucketConfiguration
                {
                    PoolConfiguration = PoolConfiguration,
                }}
            };
            Servers = new List<Uri> { Defaults.Server };

            //Set back to default
            _operationLifespanChanged = false;
            _serversChanged = false;
            _poolConfigurationChanged = false;
        }

#if NET45

        /// <summary>
        /// For synchronization with App.config or Web.configs.
        /// </summary>
        /// <param name="section"></param>
        public ClientConfiguration(CouchbaseClientSection section) : this((ICouchbaseClientDefinition) section)
        {
        }

#endif

        /// <summary>
        /// For synchronization with App.config or Web.configs.
        /// </summary>
        /// <param name="definition"></param>
        public ClientConfiguration(ICouchbaseClientDefinition definition)
        {
            Timer = TimingFactory.GetTimer();
            UseConnectionPooling = definition.UseConnectionPooling;
            EnableDeadServiceUriPing = definition.EnableDeadServiceUriPing;
            NodeAvailableCheckInterval = definition.NodeAvailableCheckInterval;
            UseSsl = definition.UseSsl;
            SslPort = definition.SslPort;
            ApiPort = definition.ApiPort;
            DirectPort = definition.DirectPort;
            MgmtPort = definition.MgmtPort;
            HttpsMgmtPort = definition.HttpsMgmtPort;
            HttpsApiPort = definition.HttpsApiPort;
            ObserveInterval = definition.ObserveInterval;
            ObserveTimeout = definition.ObserveTimeout;
            MaxViewRetries = definition.MaxViewRetries;
#pragma warning disable 618
            ViewHardTimeout = definition.ViewHardTimeout;
            SerializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            DeserializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
#pragma warning restore 618
            EnableConfigHeartBeat = definition.EnableConfigHeartBeat;
            HeartbeatConfigInterval = definition.HeartbeatConfigInterval;
            ViewRequestTimeout = definition.ViewRequestTimeout;
            Expect100Continue = definition.Expect100Continue;
            DefaultConnectionLimit = definition.DefaultConnectionLimit;
            MaxServicePointIdleTime = definition.MaxServicePointIdleTime;
            EnableOperationTiming = definition.EnableOperationTiming;
            DefaultOperationLifespan = definition.OperationLifespan;
            QueryFailedThreshold = definition.QueryFailedThreshold;
            QueryRequestTimeout = definition.QueryRequestTimeout;
            EnableQueryTiming = definition.EnableQueryTiming;
            SearchRequestTimeout = definition.SearchRequestTimeout;
            VBucketRetrySleepTime = definition.VBucketRetrySleepTime;

            IOErrorCheckInterval = definition.IOErrorCheckInterval;
            IOErrorThreshold = definition.IOErrorThreshold;

            //transcoders, converters, and serializers...o mai.
            Serializer = definition.Serializer != null
                ? SerializerFactory.GetSerializer(definition.Serializer)
                : SerializerFactory.GetSerializer();
            Converter = definition.Converter != null
                ? ConverterFactory.GetConverter(definition.Converter)
                : ConverterFactory.GetConverter();
            Transcoder = definition.Transcoder != null
                ? TranscoderFactory.GetTranscoder(this, definition.Transcoder)
                : TranscoderFactory.GetTranscoder(this);
            IOServiceCreator = definition.IOService != null
                ? IOServiceFactory.GetFactory(definition.IOService)
                : IOServiceFactory.GetFactory(this);

#if NETSTANDARD
            //TODO not implemented for json configs...yet, so default
             LoggerFactory = new LoggerFactory();
#endif

            //to enable tcp keep-alives
            EnableTcpKeepAlives = definition.EnableTcpKeepAlives;
            TcpKeepAliveInterval = definition.TcpKeepAliveInterval;
            TcpKeepAliveTime = definition.TcpKeepAliveTime;

            var keepAlivesChanged = EnableTcpKeepAlives != true ||
                                    TcpKeepAliveInterval != 1000 ||
                                    TcpKeepAliveTime != 2*60*60*1000;

            //The default sasl mechanism creator
            CreateSaslMechanism = SaslFactory.GetFactory();

            //NOTE: this is a global setting and applies to all instances
            IgnoreRemoteCertificateNameMismatch = definition.IgnoreRemoteCertificateNameMismatch;

            UseInterNetworkV6Addresses = definition.UseInterNetworkV6Addresses;

            List<Uri> servers;
            if (!string.IsNullOrEmpty(definition.ServerResolverType))
            {
                servers = ServerResolverUtil.GetServers(definition.ServerResolverType);
            }
            else if (definition.Servers != null && definition.Servers.Any())
            {
                servers = definition.Servers.ToList();
            }
            else
            {
                servers = new List<Uri> {Defaults.Server};
            }

            Servers = servers;
            _serversChanged = true;

            if (definition.ConnectionPool != null)
            {
                ConnectionPoolCreator = definition.ConnectionPool.Type != null
                    ? ConnectionPoolFactory.GetFactory(definition.ConnectionPool.Type)
                    : ConnectionPoolFactory.GetFactory();

                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = definition.ConnectionPool.MaxSize,
                    MinSize = definition.ConnectionPool.MinSize,
                    WaitTimeout = definition.ConnectionPool.WaitTimeout,
                    ShutdownTimeout = definition.ConnectionPool.ShutdownTimeout,
                    UseSsl = UseSsl ? UseSsl : definition.ConnectionPool.UseSsl,
                    BufferSize = definition.ConnectionPool.BufferSize,
                    BufferAllocator = (p) => new BufferAllocator(p.MaxSize*p.BufferSize, p.BufferSize),
                    ConnectTimeout = definition.ConnectionPool.ConnectTimeout,
                    SendTimeout = definition.ConnectionPool.SendTimeout,
                    EnableTcpKeepAlives =
                        keepAlivesChanged ? EnableTcpKeepAlives : definition.ConnectionPool.EnableTcpKeepAlives,
                    TcpKeepAliveInterval =
                        keepAlivesChanged ? TcpKeepAliveInterval : definition.ConnectionPool.TcpKeepAliveInterval,
                    TcpKeepAliveTime = keepAlivesChanged ? TcpKeepAliveTime : definition.ConnectionPool.TcpKeepAliveTime,
                    CloseAttemptInterval = definition.ConnectionPool.CloseAttemptInterval,
                    MaxCloseAttempts = definition.ConnectionPool.MaxCloseAttempts,
                    ClientConfiguration = this
                };
            }
            else
            {
                ConnectionPoolCreator = ConnectionPoolFactory.GetFactory();
                PoolConfiguration = new PoolConfiguration(this);
            }

            BucketConfigs = new Dictionary<string, BucketConfiguration>();
            if (definition.Buckets != null)
            {
                foreach (var bucket in definition.Buckets)
                {
                    var bucketConfiguration = new BucketConfiguration
                    {
                        BucketName = bucket.Name,
                        UseSsl = bucket.UseSsl,
                        Password = bucket.Password,
                        ObserveInterval = bucket.ObserveInterval,
                        DefaultOperationLifespan = bucket.OperationLifespan ?? (uint) DefaultOperationLifespan,
                        ObserveTimeout = bucket.ObserveTimeout,
                        UseEnhancedDurability = bucket.UseEnhancedDurability
                    };

                    //By skipping the bucket specific connection pool settings we allow inheritance from clien-wide connection pool settings.
                    if (bucket.ConnectionPool != null)
                    {
                        bucketConfiguration.PoolConfiguration = new PoolConfiguration
                        {
                            MaxSize = bucket.ConnectionPool.MaxSize,
                            MinSize = bucket.ConnectionPool.MinSize,
                            WaitTimeout = bucket.ConnectionPool.WaitTimeout,
                            ShutdownTimeout = bucket.ConnectionPool.ShutdownTimeout,
                            UseSsl = bucket.ConnectionPool.UseSsl,
                            BufferSize = bucket.ConnectionPool.BufferSize,
                            BufferAllocator = (p) => new BufferAllocator(p.MaxSize*p.BufferSize, p.BufferSize),
                            ConnectTimeout = bucket.ConnectionPool.ConnectTimeout,
                            SendTimeout = bucket.ConnectionPool.SendTimeout,
                            EnableTcpKeepAlives =
                                keepAlivesChanged ? EnableTcpKeepAlives : bucket.ConnectionPool.EnableTcpKeepAlives,
                            TcpKeepAliveInterval =
                                keepAlivesChanged ? TcpKeepAliveInterval : bucket.ConnectionPool.TcpKeepAliveInterval,
                            TcpKeepAliveTime =
                                keepAlivesChanged ? TcpKeepAliveTime : bucket.ConnectionPool.TcpKeepAliveTime,
                            CloseAttemptInterval = bucket.ConnectionPool.CloseAttemptInterval,
                            MaxCloseAttempts = bucket.ConnectionPool.MaxCloseAttempts,
                            UseEnhancedDurability = bucket.UseEnhancedDurability,
                            ClientConfiguration = this
                        };
                    }
                    else
                    {
                        bucketConfiguration.PoolConfiguration = PoolConfiguration;
                        bucketConfiguration.PoolConfiguration.UseSsl = bucketConfiguration.UseSsl;
                        bucketConfiguration.PoolConfiguration.UseEnhancedDurability = bucketConfiguration.UseEnhancedDurability;
                    }
                    BucketConfigs.Add(bucket.Name, bucketConfiguration);
                }
            }

            //Set back to default
            _operationLifespanChanged = false;
            _poolConfigurationChanged = false;
        }

        /// <summary>
        /// Indicates if the client should use connection pooling instead of a multiplexing connection. Defaults to false.
        /// </summary>
        public bool UseConnectionPooling { get; set; }

        /// <summary>
        /// Indicates if the client should monitor down services using ping requests and reactivate when they
        /// are back online.  Pings every <see cref="NodeAvailableCheckInterval"/>ms.  Defaults to true.
        /// </summary>
        public bool EnableDeadServiceUriPing { get; set; }

        /// <summary>
        /// Gets or sets the VBucket retry sleep time: the default is 100ms.
        /// </summary>
        /// <value>
        /// The VBucket retry sleep time.
        /// </value>
        public uint VBucketRetrySleepTime { get; set; }

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
        public int QueryFailedThreshold { get; set; }

        /// <summary>
        /// Gets or sets the timeout for a N1QL query request; this correlates to the client-side timeout.
        /// Server-side timeouts are configured per request using the <see cref="QueryRequest.Timeout"/> method.
        /// </summary>
        /// <value>
        /// The query request timeout.
        /// </value>
        /// <remarks>The value must be positive.</remarks>
        /// <remarks>The default client-side value is 75 seconds.</remarks>
        /// <remarks>The default server-side timeout is zero; this is an infinite timeout.</remarks>
        public uint QueryRequestTimeout { get; set; }

        /// <summary>
        /// Gets or sets whether the elasped client time, elasped cluster time and query statement for a N1QL query requst are written to the log appender. Disabled by default.
        /// </summary>
        /// <remarks>When enabled will cause severe performance degradation.</remarks>
        /// <remarks>Requires a <see cref="LogLevel"/>of INFO to be enabled as well.</remarks>
        public bool EnableQueryTiming { get; set; }

        /// <summary>
        /// Gets or sets the search request timeout.
        /// </summary>
        /// <value>
        /// The search request timeout.
        /// </value>
        public uint SearchRequestTimeout { get; set; }

        /// <summary>
        /// If the client detects that a node has gone offline it will check for connectivity at this interval.
        /// </summary>
        /// <remarks>The default is 1000ms.</remarks>
        /// <value>
        /// The node available check interval.
        /// </value>
        public uint NodeAvailableCheckInterval { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether enable TCP keep alives.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable TCP keep alives; otherwise, <c>false</c>.
        /// </value>
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
        /// If TLS/SSL is enabled via <see cref="UseSsl"/> setting  this to <c>true</c> will disable hostname validation when authenticating
        /// connections to Couchbase Server. This is typically done in test or development enviroments where a domain name (FQDN) has not been
        /// specified for the bootstrap uri's <see cref="Servers"/> and the IP address is used to validate the certificate, which will fail with
        /// a RemoteCertificateNameMismatch error.
        /// </summary>
        /// <value>
        /// <c>true</c> to ignore hostname validation of the certificate if you are using IP's and not a FQDN to bootstrap; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>Note: this is a global setting - it applies to all <see cref="ICluster"/> and <see cref="IBucket"/> references within a process.</remarks>
        public static bool IgnoreRemoteCertificateNameMismatch { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether use IP version 6 addresses.
        /// </summary>
        /// <value>
        /// <c>true</c> if <c>true</c> IP version 6 addresses will be used; otherwise, <c>false</c>.
        /// </value>
        public static bool UseInterNetworkV6Addresses { get; set; }

        /// <summary>
        /// Gets or sets the count of IO errors within a specific interval defined by the value of <see cref="IOErrorCheckInterval" />.
        /// If the threshold is reached within the interval for a particular node, all keys mapped to that node the SDK will fail
        /// with a <see cref="NodeUnavailableException" /> in the <see cref="IOperationResult.Exception"/> field.. The node will be flagged as "dead"
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
        public uint IOErrorThreshold { get; set; }

        /// <summary>
        /// Gets or sets the interval that the <see cref="IOErrorThreshold"/> will be checked. If the threshold is reached
        /// within the interval for a particular node, all keys mapped to that node the SDK will fail with a <see cref="NodeUnavailableException" />
        /// in the <see cref="IOperationResult.Exception"/> field. The node will be flagged as "dead" and will try to reconnect,
        /// if connectivity is reached, the node will continue to process requests.
        /// </summary>
        /// <value>
        /// The io error check interval.
        /// </value>
        /// <remarks>The purpose of this is to distinguish between a remote host being unreachable or temporay network glitch.</remarks>
        /// <remarks>The default is 500ms; use milliseconds to override this: 1000 = 1 second.</remarks>
        public uint IOErrorCheckInterval { get; set; }

        /// <summary>
        /// Gets or sets the transcoder factory.
        /// </summary>
        /// <value>
        /// The transcoder factory.
        /// </value>
        [JsonIgnore]
        public Func<ITypeTranscoder> Transcoder { get; set; }

        /// <summary>
        /// Gets or sets the converter.
        /// </summary>
        /// <value>
        /// The converter.
        /// </value>
        [JsonIgnore]
        public Func<IByteConverter> Converter { get; set; }

        /// <summary>
        /// Gets or sets the serializer.
        /// </summary>
        /// <value>
        /// The serializer.
        /// </value>
        [JsonIgnore]
        public Func<ITypeSerializer> Serializer { get; set; }

        /// <summary>
        /// Gets or sets the transporter for IO.
        /// </summary>
        /// <value>
        /// The transporter.
        /// </value>
        [JsonIgnore]
        public Func<IIOService> Transporter { get; set; }

        /// <summary>
        /// A factory for creating <see cref="IOperationTimer"/>'s.
        /// </summary>
        [JsonIgnore]
        public Func<TimingLevel, object, IOperationTimer> Timer { get; set; }

        /// <summary>
        /// A factory for creating the <see cref="IIOService"/> for this instance.
        /// </summary>
        /// <value>
        /// The io service.
        /// </value>
        [JsonIgnore]
        public Func<IConnectionPool, IIOService> IOServiceCreator { get; set; }

        /// <summary>
        /// Gets or sets the connection pool creator.
        /// </summary>
        /// <value>
        /// The connection pool creator.
        /// </value>
        [JsonIgnore]
        public Func<PoolConfiguration, IPEndPoint, IConnectionPool> ConnectionPoolCreator { get; set; }

        /// <summary>
        /// Gets or sets the create sasl mechanism.
        /// </summary>
        /// <value>
        /// The create sasl mechanism.
        /// </value>
        [JsonIgnore]
        internal Func<string, string, IIOService, ITypeTranscoder, ISaslMechanism> CreateSaslMechanism { get; set; }

#if NETSTANDARD
        public ILoggerFactory LoggerFactory { get; set; }
#endif

        /// <summary>
        /// Set to true to use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>Set to true to require all buckets to use SSL.</remarks>
        /// <remarks>Set to false and then set UseSSL at the individual Bucket level to use SSL on specific buckets.</remarks>
        public bool UseSsl
        {
            get { return _useSsl; }
            set
            {
                _useSsl = value;
                _useSslChanged = true;
            }
        }

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
        /// Gets or Sets the max time an observe operation will take before timing out.
        /// </summary>
        public int ObserveTimeout { get; set; }

        /// <summary>
        /// Gets or Sets the interval between each observe attempt.
        /// </summary>
        public int ObserveInterval { get; set; }

        /// <summary>
        /// The upper limit for the number of times a View request that has failed will be retried.
        /// </summary>
        /// <remarks>Note that not all failures are re-tried</remarks>
        public int MaxViewRetries
        {
            get { return _maxViewRetries; }
            set
            {
                if (value > -1)
                {
                    _maxViewRetries = value;
                }
            }
        }

        /// <summary>
        /// The maximum amount of time that a View will request take before timing out. Note this includes time for retries, etc.
        /// </summary>
        /// <remarks>Default is 30000ms</remarks>
        [Obsolete("Use ClientConfiguration.ViewRequestTimeout")]
        public int ViewHardTimeout
        {
            get { return _viewHardTimeout; }
            set
            {
                if (value > -1)
                {
                    _viewHardTimeout = value;
                }
            }
        }

        /// <summary>
        /// A list of hosts used to bootstrap from.
        /// </summary>
        public List<Uri> Servers
        {
            get { return _servers; }
            set
            {
                _servers = value;
                _serversChanged = true;
            }
        }

        /// <summary>
        /// The incoming serializer settings for the JSON serializer.
        /// </summary>
        [Obsolete("Please use a custom ITypeSerializer instead; this property is no longer used will be removed in a future release. See NCBC-676 for details.")]
        public JsonSerializerSettings SerializationSettings { get; set; }

        /// <summary>
        /// The outgoing serializer settings for the JSON serializer.
        /// </summary>
        [Obsolete("Please use a custom ITypeSerializer instead; this property is no longer used will be removed in a future release. See NCBC-676 for details.")]
        public JsonSerializerSettings DeserializationSettings { get; set; }

        /// <summary>
        /// A map of <see cref="BucketConfiguration"/>s and their names.
        /// </summary>
        public Dictionary<string, BucketConfiguration> BucketConfigs { get; set; }

        /// <summary>
        /// The configuration used for creating the <see cref="IConnectionPool"/> for each <see cref="IBucket"/>.
        /// </summary>
        public PoolConfiguration PoolConfiguration
        {
            get { return _poolConfiguration; }
            set
            {
                _poolConfiguration = value;
                _poolConfigurationChanged = true;
            }
        }

        /// <summary>
        /// Sets the interval for configuration "heartbeat" checks, which check for changes in the configuration that are otherwise undetected by the client.
        /// </summary>
        /// <remarks>The default is 10000ms.</remarks>
        public double HeartbeatConfigInterval
        {
            get { return _heartbeatConfigInterval; }
            set
            {
                if (value > 0 && value < Int32.MaxValue)
                {
                    _heartbeatConfigInterval = value;
                }
            }
        }

        /// <summary>
        /// Sets the timeout for each HTTP View request.
        /// </summary>
        /// <remarks>The default is 75000ms.</remarks>
        /// <remarks>The value must be greater than Zero.</remarks>
        public int ViewRequestTimeout
        {
            get { return _viewRequestTimeout; }
            set
            {
                if (value > 0)
                {
                    _viewRequestTimeout = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections allowed by a ServicePoint object used for making View and N1QL requests.
        /// </summary>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.defaultconnectionlimit.aspx</remarks>
        /// <remarks>The default is set to 5 connections.</remarks>
#if NETSTANDARD
        /// <remarks>On Linux this setting is only supported for libcurl v7.30.0 and greater. See https://issues.couchbase.com/browse/NCBC-1295 for details.</remarks>
#endif
        public int DefaultConnectionLimit { get; set; }

        /// <summary>
        /// Gets or sets the maximum idle time of a ServicePoint object used for making View and N1QL requests.
        /// </summary>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.maxservicepointidletime.aspx</remarks>
        [Obsolete("The SDK uses HttpWinHandler and HttpRequestHandler which do not support the ServicePointManager.")]
        public int MaxServicePointIdleTime { get; set; }

        /// <summary>
        /// Gets or sets a Boolean value that determines whether 100-Continue behavior is used.
        /// </summary>
        /// <remarks>The default is false, which overrides the <see cref="ServicePointManager"/>'s default of true.</remarks>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.expect100continue%28v=vs.110%29.aspx</remarks>
        public bool Expect100Continue { get; set; }

        /// <summary>
        /// Enables configuration "heartbeat" checks.
        /// </summary>
        /// <remarks>The default is "enabled" or true.</remarks>
        /// <remarks>The interval of the configuration hearbeat check is controlled by the <see cref="HeartbeatConfigInterval"/> property.</remarks>
        public bool EnableConfigHeartBeat { get; set; }

        /// <summary>
        /// Writes the elasped time for an operation to the log appender Disabled by default.
        /// </summary>
        /// <remarks>When enabled will cause severe performance degradation.</remarks>
        /// <remarks>Requires a <see cref="LogLevel"/>of DEBUG to be enabled as well.</remarks>
        public bool EnableOperationTiming { get; set; }

        /// <summary>
        /// The size of each buffer to allocate per TCP connection for sending and recieving Memcached operations
        /// </summary>
        /// <remarks>The default is 16K</remarks>
        /// <remarks>The total buffer size is BufferSize * PoolConfiguration.MaxSize</remarks>
        public int BufferSize { get; set; }

        /// <summary>
        /// The maximum time allowed for an operation to live, in milliseconds. This servers as the default
        /// for buckets where the lifespan is not explicitely specified.
        /// </summary>
        /// <remarks>The default is 2500 (2.5 seconds)</remarks>
        /// <remarks>When getting the value, prefer looking in <see cref="BucketConfiguration.DefaultOperationLifespan"/>
        /// since it will inherit and possibly overwrite this value.</remarks>
        public uint DefaultOperationLifespan
        {
            get { return _operationLifespan; }
            set
            {
                _operationLifespan = value;
                _operationLifespanChanged = true;
            }
        }

        /// <summary>
        /// Updates the internal bootstrap url with the new list from a server configuration.
        /// </summary>
        /// <param name="bucketConfig">A new server configuration</param>
        internal void UpdateBootstrapList(IBucketConfig bucketConfig)
        {
            foreach (var node in bucketConfig.Nodes)
            {
                var uri = new Uri(string.Concat("http://", node.Hostname, "/pools"));
                ConfigLock.EnterWriteLock();
                try
                {
                    if (!Servers.Contains(uri))
                    {
                        Servers.Add(uri);
                    }
                }
                finally
                {
                    ConfigLock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Checks for mutations of the Server collection
        /// </summary>
        /// <returns></returns>
        internal bool HasServersChanged()
        {
            //The list has already been modified via initializer
            if (_serversChanged)  return true;

            //The list has changed via Add()
            if (Servers.Count > 1) return true;

            var uri = Servers.FirstOrDefault();
            if (uri == null)
            {
                const string msg = "One server is required for bootstrapping!";
                throw new ArgumentNullException(msg);
            }
            return uri.OriginalString != "http://localhost:8091/pools";
        }

        /// <summary>
        /// Gets the analytics request timeout. Default is 75 seconds.
        /// </summary>
        /// <value>
        /// The analytics request timeout.
        /// </value>
        /// <remarks>Hardcoded for now - will implement config at a later time</remarks>
        public uint AnalyticsRequestTimeout
        {
            get { return 75000; }
        }

        internal void Initialize()
        {
#if NETSTANDARD
            //configure logging
            LogManager.ConfigureLoggerFactory(LoggerFactory);
#endif
            if (PoolConfiguration == null)
            {
                PoolConfiguration = new PoolConfiguration(this);
            }
            if (PoolConfiguration.ClientConfiguration == null)
            {
                PoolConfiguration.ClientConfiguration = this;
            }
            if (TcpKeepAliveTime != Defaults.TcpKeepAliveTime &&
                PoolConfiguration.TcpKeepAliveTime == Defaults.TcpKeepAliveTime)
            {
                PoolConfiguration.TcpKeepAliveTime = TcpKeepAliveTime;
            }
            if (TcpKeepAliveInterval != Defaults.TcpKeepAliveInterval &&
                PoolConfiguration.TcpKeepAliveInterval == Defaults.TcpKeepAliveInterval)
            {
                PoolConfiguration.TcpKeepAliveInterval = TcpKeepAliveInterval;
            }
            if (EnableTcpKeepAlives != Defaults.EnableTcpKeepAlives &&
                PoolConfiguration.EnableTcpKeepAlives == Defaults.EnableTcpKeepAlives)
            {
                PoolConfiguration.EnableTcpKeepAlives = EnableTcpKeepAlives;
            }

            if (_serversChanged)
            {
                for (var i = 0; i < _servers.Count(); i++)
                {
                    if (!_servers[i].OriginalString.EndsWith("/pools") || _servers[i].Port == 80)
                    {
                        var builder = new UriBuilder(_servers[i])
                        {
                            Port = _servers[i].Port == 80 ? 8091 : _servers[i].Port,
                            Path = _servers[i].OriginalString.EndsWith("/") ? "pools" : "/pools"
                        };
                        _servers[i] = builder.Uri;
                    }
                }
            }

            //Update the bucket configs
            foreach (var keyValue in BucketConfigs)
            {
                var bucketConfiguration = keyValue.Value;
                if (string.IsNullOrEmpty(bucketConfiguration.BucketName))
                {
                    if (string.IsNullOrWhiteSpace(keyValue.Key))
                    {
                        throw new ArgumentException("bucketConfiguration.BucketName is null or empty.");
                    }
                    bucketConfiguration.BucketName = keyValue.Key;
                }
                if (bucketConfiguration.PoolConfiguration == null || _poolConfigurationChanged)
                {
                    bucketConfiguration.PoolConfiguration = PoolConfiguration;
                }
                if (bucketConfiguration.Servers == null || HasServersChanged())
                {
                    bucketConfiguration.Servers = Servers.Select(x => x).ToList();
                }
                if (bucketConfiguration.Servers.Count == 0)
                {
                    bucketConfiguration.Servers.AddRange(Servers.Select(x => x).ToList());
                }
                if (bucketConfiguration.Port == (int)DefaultPorts.Proxy)
                {
                    var message = string.Format("Proxy port {0} is not supported by the .NET client.",
                        bucketConfiguration.Port);
                    throw new NotSupportedException(message);
                }
                if (bucketConfiguration.UseSsl)
                {
                    bucketConfiguration.PoolConfiguration.UseSsl = true;
                }
                if (UseSsl)
                {
                    //Setting ssl to true at parent level overrides child level ssl settings
                    bucketConfiguration.UseSsl = true;
                    bucketConfiguration.Port = SslPort;
                    bucketConfiguration.PoolConfiguration.UseSsl = true;
                }
                if (_useSslChanged)
                {
                    for (var i = 0; i < _servers.Count(); i++)
                    {
                        var useSsl = UseSsl || bucketConfiguration.UseSsl;
                        //Rewrite the URI's for bootstrapping to use SSL.
                        if (useSsl)
                        {
                            var oldUri = _servers[i];
                            var newUri = new Uri(string.Concat("https://", _servers[i].Host,
                                ":", HttpsMgmtPort, oldUri.PathAndQuery));
                            _servers[i] = newUri;
                        }
                    }
                }
                //operation lifespan: if it has changed at bucket level, use bucket level, else use global level
                if (_operationLifespanChanged)
                {
                    bucketConfiguration.UpdateOperationLifespanDefault(_operationLifespan);
                }
            }
        }

        internal void SetAuthenticator(IAuthenticator authenticator)
        {
            if (authenticator == null)
            {
                throw new ArgumentNullException("authenticator");
            }

            string username;
            if (TryGetUsernameFromConnectionString(out username))
            {
                switch (authenticator.AuthenticatorType)
                {
                    case AuthenticatorType.Classic:
                        var classicAuthenticator = (ClassicAuthenticator) authenticator;
                        if (string.IsNullOrWhiteSpace(classicAuthenticator.ClusterUsername))
                        {
                            classicAuthenticator.ClusterUsername = username;
                        }
                        break;
                    case AuthenticatorType.Password:
                        var passwordAuthenticator = (PasswordAuthenticator) authenticator;
                        if (string.IsNullOrWhiteSpace(passwordAuthenticator.Username))
                        {
                            passwordAuthenticator.Username = username;
                        }
                        break;
                }
            }

            Authenticator = authenticator;
        }

        private bool TryGetUsernameFromConnectionString(out string username)
        {
            var server = Servers.FirstOrDefault();
            if (server == null) // no servers available to read from
            {
                username = null;
                return false;
            }

            // check if the username part is available
            var index = server.UserInfo.IndexOf(":", StringComparison.OrdinalIgnoreCase);
            username = index >= 0 ? server.UserInfo.Substring(0, index) : server.UserInfo;
            return !string.IsNullOrWhiteSpace(username);
        }

        internal IAuthenticator Authenticator { get; private set; }

        internal bool HasCredentials
        {
            get { return Authenticator != null; }
        }

        internal IDictionary<string, string> GetCredentials(AuthContext context)
        {
            return Authenticator.GetCredentials(context);
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
