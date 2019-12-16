using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTracing;

namespace Couchbase
{
    public sealed class ClusterOptions
    {
        private ConcurrentBag<Uri> _servers = new ConcurrentBag<Uri>();
        private ConcurrentBag<string> _buckets = new ConcurrentBag<string>();
        internal ConnectionString ConnectionString { get; set; }
        public string connectionString { get; set; }

        public static bool UseInterNetworkV6Addresses { get; set; }

        public ClusterOptions WithConnectionString(string connectionString)
        {
            ConnectionString = ConnectionString.Parse(connectionString);
            var uriBuilders = ConnectionString.Hosts.Select(x => new UriBuilder
            {
                Host = x,
                Port = KvPort
            }.Uri).ToArray();
            WithServers(uriBuilders);
            return this;
        }

        public ClusterOptions WithServers(params string[] servers)
        {
            if (!servers?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(servers)} cannot be null or empty.");
            }

            //for now just copy over - later ensure only new nodes are added
            _servers = new ConcurrentBag<Uri>(servers.Select(x => new Uri(x)));
            return this;
        }

        internal ClusterOptions WithServers(IEnumerable<Uri> servers)
        {
            if (!servers?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(servers)} cannot be null or empty.");
            }

            //for now just copy over - later ensure only new nodes are added
            _servers = new ConcurrentBag<Uri>(servers.ToList());
            return this;
        }

        public ClusterOptions WithBucket(params string[] bucketNames)
        {
            if (!bucketNames?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(bucketNames)} cannot be null or empty.");
            }

            //just the name of the bucket for now - later make and actual cluster
            _buckets = new ConcurrentBag<string>(bucketNames.ToList());
            return this;
        }

        public ClusterOptions WithCredentials(string username, string password)
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

        public ClusterOptions WithLogging(ILoggerFactory loggerFactory = null)
        {
            //configure a null logger as the default
            if (loggerFactory == null)
            {
                LogManager.LoggerFactory = new LoggerFactory(new[]
                {
                    NullLoggerProvider.Instance
                });
                return this;
            }

            LogManager.LoggerFactory = loggerFactory;
            return this;
        }

        public IEnumerable<Uri> Servers => _servers;
        public IEnumerable<string> Buckets => _buckets;
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

            if (ConnectionString.Scheme != Scheme.Couchbase && ConnectionString.Scheme != Scheme.Couchbases)
            {
                return false;
            }

            if (ConnectionString.Hosts.Count > 1)
            {
                return false;
            }

            return ConnectionString.Hosts.Single().IndexOf(":") == -1;
        }
    }

    public static class NetworkTypes
    {
        public const string Auto = "auto";
        public const string Default = "default";
        public const string External = "external";
    }
}
