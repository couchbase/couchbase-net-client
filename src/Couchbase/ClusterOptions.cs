using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTracing;

using ConnectionStringCls = Couchbase.ConnectionString;

namespace Couchbase
{
    public sealed class ClusterOptions
    {
        private ConcurrentBag<Uri> _servers = new ConcurrentBag<Uri>();
        private ConcurrentBag<string> _buckets = new ConcurrentBag<string>();
        internal ConnectionString ConnectionStringValue { get; set; }
        internal string connectionString { get; set; }

        public static bool UseInterNetworkV6Addresses { get; set; }

        public ClusterOptions ConnectionString(string connectionString)
        {
            ConnectionStringValue = ConnectionStringCls.Parse(connectionString);
            var uriBuilders = ConnectionStringValue.Hosts.Select(x => new UriBuilder
            {
                Host = x,
                Port = KvPort
            }.Uri).ToArray();
            Servers(uriBuilders);
            return this;
        }

        public ClusterOptions Servers(params string[] servers)
        {
            if (!servers?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(servers)} cannot be null or empty.");
            }

            //for now just copy over - later ensure only new nodes are added
            _servers = new ConcurrentBag<Uri>(servers.Select(x => new Uri(x)));
            return this;
        }

        internal ClusterOptions Servers(IEnumerable<Uri> servers)
        {
            if (!servers?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(servers)} cannot be null or empty.");
            }

            //for now just copy over - later ensure only new nodes are added
            _servers = new ConcurrentBag<Uri>(servers.ToList());
            return this;
        }

        public ClusterOptions Bucket(params string[] bucketNames)
        {
            if (!bucketNames?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(bucketNames)} cannot be null or empty.");
            }

            //just the name of the bucket for now - later make and actual cluster
            _buckets = new ConcurrentBag<string>(bucketNames.ToList());
            return this;
        }

        public ClusterOptions Credentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException($"{nameof(username)} cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException($"{nameof(password)} cannot be null or empty.");
            }

            UserName = username;
            Password = password;
            return this;
        }

        public ClusterOptions Logging(ILoggerFactory loggerFactory = null)
        {
            //configure a null logger as the default
            if (loggerFactory == null)
            {
                LogManager.LoggerFactory = new NullLoggerFactory();
                return this;
            }

            LogManager.LoggerFactory = loggerFactory;

            // TODO: Eliminate LogManager, only use DI for logging
            AddSingletonService(loggerFactory);

            return this;
        }

        internal IEnumerable<Uri> ServersValue => _servers;
        internal IEnumerable<string> Buckets => _buckets;
        public string UserName { get; set; }
        public string Password { get; set; }

        //Foundation RFC conformance
        public TimeSpan KvConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan KvTimeout { get; set; } = TimeSpan.FromSeconds(2.5);
        public TimeSpan KvDurabilityTimeout { get; set; } = TimeSpan.FromSeconds(10);
        public TimeSpan ViewTimeout { get; set; } = TimeSpan.FromSeconds(75);
        public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(75);
        public TimeSpan AnalyticsTimeout { get; set; } = TimeSpan.FromSeconds(75);
        public TimeSpan SearchTimeout { get; set; } = TimeSpan.FromSeconds(75);
        public TimeSpan ManagementTimeout { get; set; } = TimeSpan.FromSeconds(75);
        public bool EnableTls { get; set; }
        public ITypeTranscoder Transcoder { get; set; } = new DefaultTranscoder();
        public ITypeSerializer JsonSerializer { get; set; } = new DefaultSerializer();
        public bool EnableMutationTokens { get; set; } = true;
        public ITracer Tracer = new ThresholdLoggingTracer();
        public TimeSpan TcpKeepAliveTime { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan TcpKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(1);
        public bool ForceIPv4 { get; set; }
        public TimeSpan ConfigPollInterval { get; set; } = TimeSpan.FromSeconds(2.5);
        public TimeSpan ConfigPollFloorInterval { get; set; } = TimeSpan.FromMilliseconds(50);
        public TimeSpan ConfigIdleRedialTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public int NumKvConnections { get; set; } = 1;
        public int MaxHttpConnection { get; set; } = 0;
        public TimeSpan IdleHttpConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        public CircuitBreakerConfiguration CircuitBreakerConfiguration { get; set; } =
            CircuitBreakerConfiguration.Default;

        //Volatile or obsolete options
        public int MgmtPort { get; set; } = 8091;
        public bool Expect100Continue { get; set; }
        public bool EnableCertificateAuthentication { get; set; }
        public bool EnableCertificateRevocation { get; set; }
        public bool IgnoreRemoteCertificateNameMismatch { get; set; }
        public bool OrphanedResponseLoggingEnabled { get; set; }
        public bool EnableConfigPolling { get; set; } = true;
        public bool EnableTcpKeepAlives { get; set; } = true;
        public bool EnableIPV6Addressing { get; set; }
        public int KvPort { get; set; } = 11210;
        public bool EnableDnsSrvResolution { get; set; } = true;
        public IDnsResolver DnsResolver { get; set; } = new DnsClientDnsResolver();

        internal bool IsValidDnsSrv()
        {
            if (!EnableDnsSrvResolution || DnsResolver == null)
            {
                return false;
            }

            if (ConnectionStringValue.Scheme != Scheme.Couchbase && ConnectionStringValue.Scheme != Scheme.Couchbases)
            {
                return false;
            }

            if (ConnectionStringValue.Hosts.Count > 1)
            {
                return false;
            }

            return ConnectionStringValue.Hosts.Single().IndexOf(":") == -1;
        }

        #region DI

        private readonly IDictionary<Type, IServiceFactory> _services = DefaultServices.GetDefaultServices();

        /// <summary>
        /// Build a <see cref="IServiceProvider"/> from the currently registered services.
        /// </summary>
        /// <returns>The new <see cref="IServiceProvider"/>.</returns>
        internal IServiceProvider BuildServiceProvider() =>
            new CouchbaseServiceProvider(_services);

        internal void AddTransientService<T>(Func<IServiceProvider, T> factory)
        {
            _services[typeof(T)] = new LambdaServiceFactory(serviceProvider => factory(serviceProvider));
        }

        internal void AddSingletonService<T>(T singleton)
        {
            _services[typeof(T)] = new SingletonServiceFactory(singleton);
        }

        #endregion
    }

    public static class NetworkTypes
    {
        public const string Auto = "auto";
        public const string Default = "default";
        public const string External = "external";
    }
}
