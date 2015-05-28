﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// Represents the configuration of a <see cref="Cluster"/> object. The <see cref="Cluster"/> object
    /// will use this class to construct it's internals.
    /// </summary>
    public class ClientConfiguration
    {
        private static readonly ILog Log = LogManager.GetLogger<ClientConfiguration>();
        protected ReaderWriterLockSlim ConfigLock = new ReaderWriterLockSlim();
        private const string DefaultBucket = "default";
        private readonly Uri _defaultServer = new Uri("http://localhost:8091/pools");
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

        public ClientConfiguration()
        {
            //For operation timing
            Timer = TimingFactory.GetTimer(Log);

            UseSsl = false;
            SslPort = 11207;
            ApiPort = 8092;
            DirectPort = 11210;
            MgmtPort = 8091;
            HttpsMgmtPort = 18091;
            HttpsApiPort = 18092;
            ObserveInterval = 10; //ms
            ObserveTimeout = 500; //ms
            MaxViewRetries = 2;
            ViewHardTimeout = 30000; //ms
            HeartbeatConfigInterval = 10000; //ms
            EnableConfigHeartBeat = true;
            SerializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            DeserializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            ViewRequestTimeout = 5000; //ms
            DefaultConnectionLimit = 5; //connections
            Expect100Continue = false;
            EnableOperationTiming = false;
            BufferSize = 1024 * 16;
            DefaultOperationLifespan = 2500;//ms
            EnableTcpKeepAlives = true;

            TcpKeepAliveTime = 2*60*60*1000;
            TcpKeepAliveInterval = 1000;

            NodeAvailableCheckInterval = 1000;//ms

            //the default serializer
            Serializer = SerializerFactory.GetSerializer();

            //the default byte converter
            Converter = ConverterFactory.GetConverter();

            //the default transcoder
            Transcoder = TranscoderFactory.GetTranscoder(this);

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
            Servers = new List<Uri> { _defaultServer };

            //Set back to default
            _operationLifespanChanged = false;
            _serversChanged = false;
            _poolConfigurationChanged = false;
        }

        /// <summary>
        /// For synchronization with App.config or Web.configs.
        /// </summary>
        /// <param name="section"></param>
        public ClientConfiguration(CouchbaseClientSection section)
        {
            Timer = TimingFactory.GetTimer(Log);
            NodeAvailableCheckInterval = section.NodeAvailableCheckInterval;
            UseSsl = section.UseSsl;
            SslPort = section.SslPort;
            ApiPort = section.ApiPort;
            DirectPort = section.DirectPort;
            MgmtPort = section.MgmtPort;
            HttpsMgmtPort = section.HttpsMgmtPort;
            HttpsApiPort = section.HttpsApiPort;
            ObserveInterval = section.ObserveInterval;
            ObserveTimeout = section.ObserveTimeout;
            MaxViewRetries = section.MaxViewRetries;
            ViewHardTimeout = section.ViewHardTimeout;
            SerializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            DeserializationSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            EnableConfigHeartBeat = section.EnableConfigHeartBeat;
            HeartbeatConfigInterval = section.HeartbeatConfigInterval;
            ViewRequestTimeout = section.ViewRequestTimeout;
            Expect100Continue = section.Expect100Continue;
            EnableOperationTiming = section.EnableOperationTiming;
            DefaultOperationLifespan = section.OperationLifespan;

            //transcoders, converters, and serializers...o mai.
            Serializer = SerializerFactory.GetSerializer(this, section.Serializer);
            Converter = ConverterFactory.GetConverter(section.Converter);
            Transcoder = TranscoderFactory.GetTranscoder(this, section.Transcoder);

            //to enable tcp keep-alives
            EnableTcpKeepAlives = section.EnableTcpKeepAlives;
            TcpKeepAliveInterval = section.TcpKeepAliveInterval;
            TcpKeepAliveTime = section.TcpKeepAliveTime;

            var keepAlivesChanged = EnableTcpKeepAlives != true ||
                                    TcpKeepAliveInterval != 1000 ||
                                    TcpKeepAliveTime != 2*60*60*1000;

            foreach (var server in section.Servers)
            {
                Servers.Add(((UriElement)server).Uri);
                _serversChanged = true;
            }

            PoolConfiguration = new PoolConfiguration
            {
                MaxSize = section.ConnectionPool.MaxSize,
                MinSize = section.ConnectionPool.MinSize,
                WaitTimeout = section.ConnectionPool.WaitTimeout,
                ShutdownTimeout = section.ConnectionPool.ShutdownTimeout,
                UseSsl = section.ConnectionPool.UseSsl,
                BufferSize = section.ConnectionPool.BufferSize,
                BufferAllocator = (p) => new BufferAllocator(p.MaxSize * p.BufferSize, p.BufferSize),
                ConnectTimeout = section.ConnectionPool.ConnectTimeout,
                SendTimeout = section.ConnectionPool.SendTimeout,
                EnableTcpKeepAlives = keepAlivesChanged ? EnableTcpKeepAlives : section.ConnectionPool.EnableTcpKeepAlives,
                TcpKeepAliveInterval = keepAlivesChanged ? TcpKeepAliveInterval : section.ConnectionPool.TcpKeepAliveInterval,
                TcpKeepAliveTime = keepAlivesChanged ? TcpKeepAliveTime : section.ConnectionPool.TcpKeepAliveTime,
                CloseAttemptInterval = section.ConnectionPool.CloseAttemptInterval,
                MaxCloseAttempts = section.ConnectionPool.MaxCloseAttempts,
                ClientConfiguration = this
            };

            BucketConfigs = new Dictionary<string, BucketConfiguration>();
            foreach (var bucketElement in section.Buckets)
            {
                var bucket = (BucketElement)bucketElement;
                var bucketConfiguration = new BucketConfiguration
                {
                    BucketName = bucket.Name,
                    UseSsl = bucket.UseSsl,
                    Password = bucket.Password,
                    ObserveInterval = bucket.ObserveInterval,
                    DefaultOperationLifespan = bucket.OperationLifespan ??(uint) DefaultOperationLifespan,
                    ObserveTimeout = bucket.ObserveTimeout
                };
                //Configuration properties (including elements) can not be null, but we can check if it was originally presnt in xml and skip it.
                //By skipping the bucket specific connection pool settings we allow inheritance from clien-wide connection pool settings.
                if (bucket.ConnectionPool.ElementInformation.IsPresent)
                {
                    bucketConfiguration.PoolConfiguration = new PoolConfiguration
                    {
                        MaxSize = bucket.ConnectionPool.MaxSize,
                        MinSize = bucket.ConnectionPool.MinSize,
                        WaitTimeout = bucket.ConnectionPool.WaitTimeout,
                        ShutdownTimeout = bucket.ConnectionPool.ShutdownTimeout,
                        UseSsl = bucket.ConnectionPool.UseSsl,
                        BufferSize = bucket.ConnectionPool.BufferSize,
                        BufferAllocator = (p) => new BufferAllocator(p.MaxSize * p.BufferSize, p.BufferSize),
                        ConnectTimeout = bucket.ConnectionPool.ConnectTimeout,
                        SendTimeout = bucket.ConnectionPool.SendTimeout,
                        EnableTcpKeepAlives = keepAlivesChanged ? EnableTcpKeepAlives : bucket.ConnectionPool.EnableTcpKeepAlives,
                        TcpKeepAliveInterval = keepAlivesChanged ? TcpKeepAliveInterval : bucket.ConnectionPool.TcpKeepAliveInterval,
                        TcpKeepAliveTime = keepAlivesChanged ? TcpKeepAliveTime : bucket.ConnectionPool.TcpKeepAliveTime,
                        CloseAttemptInterval = bucket.ConnectionPool.CloseAttemptInterval,
                        MaxCloseAttempts = bucket.ConnectionPool.MaxCloseAttempts,
                        ClientConfiguration = this
                    };
                }
                BucketConfigs.Add(bucket.Name, bucketConfiguration);
            }

            //Set back to default
            _operationLifespanChanged = false;
            _poolConfigurationChanged = false;
        }

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
        /// A factory for creating <see cref="IOperationTimer"/>'s.
        /// </summary>
        [JsonIgnore]
        public Func<TimingLevel, object, IOperationTimer> Timer { get; set; }

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
        /// <remarks>The default is 5000ms.</remarks>
        /// <remarks>The value must be greater than Zero and less than 60000ms.</remarks>
        public int ViewRequestTimeout
        {
            get { return _viewRequestTimeout; }
            set
            {
                if (value > 0 && value < 60000)
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
        public int DefaultConnectionLimit
        {
            get { return ServicePointManager.DefaultConnectionLimit; }
            set { ServicePointManager.DefaultConnectionLimit = value; }
        }

        /// <summary>
        /// Gets or sets the maximum idle time of a ServicePoint object used for making View and N1QL requests.
        /// </summary>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.maxservicepointidletime.aspx</remarks>
        public int MaxServicePointIdleTime
        {
            get { return ServicePointManager.MaxServicePointIdleTime; }
            set { ServicePointManager.MaxServicePointIdleTime = value; }
        }

        /// <summary>
        /// Gets or sets a Boolean value that determines whether 100-Continue behavior is used.
        /// </summary>
        /// <remarks>The default is false, which overrides the <see cref="ServicePointManager"/>'s default of true.</remarks>
        /// <remarks>http://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.expect100continue%28v=vs.110%29.aspx</remarks>
        public bool Expect100Continue
        {
            get { return ServicePointManager.Expect100Continue; }
            set { ServicePointManager.Expect100Continue = value; }
        }

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

        internal void Initialize()
        {
            if (PoolConfiguration == null)
            {
                PoolConfiguration = new PoolConfiguration(this);
            }
            if (PoolConfiguration.ClientConfiguration == null)
            {
                PoolConfiguration.ClientConfiguration = this;
            }

            if (_serversChanged)
            {
                for (var i = 0; i < _servers.Count(); i++)
                {
                    if (_servers[i].OriginalString.EndsWith("/pools"))
                    {
                        /*noop*/
                    }
                    else
                    {
                        var newUri = _servers[i].ToString();
                        newUri = string.Concat(newUri, newUri.EndsWith("/") ? "pools" : "/pools");
                        _servers[i] = new Uri(newUri);
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
