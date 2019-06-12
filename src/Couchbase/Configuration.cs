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
        private ConcurrentBag<Uri>_servers = new ConcurrentBag<Uri>();
        private ConcurrentBag<string> _buckets = new ConcurrentBag<string>();
        internal ConcurrentBag<ClusterNode> GlobalNodes { get; set; } = new ConcurrentBag<ClusterNode>();

        public static bool UseInterNetworkV6Addresses { get; set; }

        public Configuration WithServers(params string[] ips)
        {
            if (ips == null)
            {
                throw new ArgumentNullException(nameof(ips));
            }

            //for now just copy over - later ensure only new nodes are added
            return new Configuration
            {
                UserName = UserName,
                Password = Password,
                _servers = new ConcurrentBag<Uri>(ips.Select(x=>new Uri(x))),
                _buckets = _buckets
            };
        }

        public Configuration WithBucket(params string[] bucketNames)
        {
            if(bucketNames == null)
            {
                throw new ArgumentNullException(nameof(bucketNames ));
            }

            //just the name of the bucket for now - later make and actual config
            return new Configuration
            {
                UserName = UserName,
                Password = Password,
                _buckets = new ConcurrentBag<string>(bucketNames.ToList()),
                _servers = _servers
            };
        }

        public Configuration WithCredentials(string username, string password)
        {
            return new Configuration
            {
                UserName = username,
                Password = password,
                _servers = _servers,
                _buckets = _buckets
            };
        }

        public Configuration WithLogging(ILoggerProvider provider = null)
        {
            //configure a null logger as the default
            if (provider == null)
            {
                provider = NullLoggerProvider.Instance;
            }
            LogManager.LoggerFactory.AddProvider(provider);
            return new Configuration
            {
                UserName = UserName,
                Password = Password,
                _servers = _servers,
                _buckets = _buckets
            };
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
