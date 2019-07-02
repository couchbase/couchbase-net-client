using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Couchbase
{
    public sealed class Configuration
    {
        private ConcurrentBag<Uri> _servers = new ConcurrentBag<Uri>();
        private ConcurrentBag<string> _buckets = new ConcurrentBag<string>();
        internal ConcurrentBag<ClusterNode> GlobalNodes { get; set; } = new ConcurrentBag<ClusterNode>();

        public static bool UseInterNetworkV6Addresses { get; set; }

        public Configuration WithServers(params string[] servers)
        {
            if (!servers?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(servers)} cannot be null or empty.");
            }

            //for now just copy over - later ensure only new nodes are added
            _servers = new ConcurrentBag<Uri>(servers.Select(x => new Uri(x)));
            return this;
        }

        public Configuration WithBucket(params string[] bucketNames)
        {
            if (!bucketNames?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(bucketNames)} cannot be null or empty.");
            }

            //just the name of the bucket for now - later make and actual config
            _buckets = new ConcurrentBag<string>(bucketNames.ToList());
            return this;
        }

        public Configuration WithCredentials(string username, string password)
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

        public Configuration WithLogging(ILoggerProvider provider = null)
        {
            //configure a null logger as the default
            if (provider == null)
            {
                provider = NullLoggerProvider.Instance;
            }

            LogManager.LoggerFactory.AddProvider(provider);
            return this;
        }

        public IEnumerable<Uri> Servers => _servers;
        public IEnumerable<string> Buckets => _buckets;
        public string UserName { get; set; }
        public string Password { get; set; }
        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan KvTimeout { get; set; }
        public TimeSpan ViewTimeout { get; set; }
        public TimeSpan QueryTimeout { get; set; }
        public TimeSpan AnalyticsTimeout { get; set; }
        public TimeSpan SearchTimeout { get; set; }
        public TimeSpan ManagementTimeout { get; set; }
        public TimeSpan ConfigPollInterval { get; set; } = TimeSpan.FromSeconds(2500);
        public bool UseSsl { get; set; }
        public bool EnableTracing { get; set; }
        public bool EnableMutationTokens { get; set; }
        public int MgmtPort { get; set; } = 8091;
        public bool Expect100Continue { get; set; }
        public bool EnableCertificateAuthentication { get; set; }
        public bool EnableCertificateRevocation { get; set; }
        public bool IgnoreRemoteCertificateNameMismatch { get; set; }
        public int MaxQueryConnectionsPerServer { get; set; } = 10;
        public bool OrphanedResponseLoggingEnabled { get; set; }
        public bool EnableConfigPolling { get; set; }
    }
}
