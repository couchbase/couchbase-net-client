using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTracing;

// ReSharper disable UnusedMember.Global

#nullable enable

namespace Couchbase
{
    public sealed class ClusterOptions
    {
        private ConcurrentBag<Uri> _servers = new ConcurrentBag<Uri>();
        private ConcurrentBag<string> _buckets = new ConcurrentBag<string>();
        internal ConnectionString? ConnectionStringValue { get; set; }

        public ClusterOptions ConnectionString(string connectionString)
        {
            if (connectionString == null)
            {
                throw new ArgumentNullException(nameof(connectionString));
            }

            ConnectionStringValue = Couchbase.ConnectionString.Parse(connectionString);
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
            var serverList = servers?.ToList();
            if (!serverList?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(servers)} cannot be null or empty.");
            }

            //for now just copy over - later ensure only new nodes are added
            _servers = new ConcurrentBag<Uri>(serverList);
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

        public ClusterOptions Logging(ILoggerFactory? loggerFactory = null)
        {
            loggerFactory ??= new NullLoggerFactory();

            AddSingletonService(loggerFactory);

            return this;
        }

        /// <summary>
        /// Provide a custom <see cref="ITypeSerializer"/>.
        /// </summary>
        /// <param name="serializer">Serializer to use.</param>
        /// <returns><see cref="ClusterOptions"/>.</returns>
        public ClusterOptions Serializer(ITypeSerializer serializer)
        {
            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            AddSingletonService(serializer);

            return this;
        }

        /// <summary>
        /// Provide a custom <see cref="ITypeTranscoder"/>.
        /// </summary>
        /// <param name="transcoder">Transcoder to use.</param>
        /// <returns><see cref="ClusterOptions"/>.</returns>
        public ClusterOptions Transcoder(ITypeTranscoder transcoder)
        {
            if (transcoder == null)
            {
                throw new ArgumentNullException(nameof(transcoder));
            }

            AddSingletonService(transcoder);

            return this;
        }

        /// <summary>
        /// Provide a custom <see cref="IDnsResolver"/> for DNS SRV resolution.
        /// </summary>
        /// <param name="dnsResolver">DNS resolver to use.</param>
        /// <returns><see cref="ClusterOptions"/>.</returns>
        public ClusterOptions DnsResolver(IDnsResolver dnsResolver)
        {
            if (dnsResolver == null)
            {
                throw new ArgumentNullException(nameof(dnsResolver));
            }

            AddSingletonService(dnsResolver);

            return this;
        }

        internal IEnumerable<Uri> ServersValue => _servers;
        internal IEnumerable<string> Buckets => _buckets;
        public string? UserName { get; set; }
        public string? Password { get; set; }

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

        public TimeSpan IdleHttpConnectionTimeout
        {
            get => throw new NotSupportedException("Not supported by .NET Core.");
            set => throw new NotSupportedException("Not supported by .NET Core.");
        }

        public CircuitBreakerConfiguration CircuitBreakerConfiguration { get; set; } =
            CircuitBreakerConfiguration.Default;

        public bool EnableOperationDurationTracing { get; set; } = true;

        //Volatile or obsolete options
        public int MgmtPort { get; set; } = 8091;
        public bool EnableExpect100Continue { get; set; }
        public bool EnableCertificateAuthentication { get; set; }
        public bool EnableCertificateRevocation { get; set; }
        public bool IgnoreRemoteCertificateNameMismatch { get; set; }

        private bool _enableOrphanedResponseLogging;
        public bool EnableOrphanedResponseLogging
        {
            get => _enableOrphanedResponseLogging;
            set
            {
                if (value != _enableOrphanedResponseLogging)
                {
                    _enableOrphanedResponseLogging = value;

                    if (value)
                    {
                        AddSingletonService<IOrphanedResponseLogger, OrphanedResponseLogger>();
                    }
                    else
                    {
                        AddSingletonService<IOrphanedResponseLogger, NullOrphanedResponseLogger>();
                    }
                }
            }
        }

        public bool EnableConfigPolling { get; set; } = true;
        public bool EnableTcpKeepAlives { get; set; } = true;
        public int KvPort { get; set; } = 11210;
        public bool EnableDnsSrvResolution { get; set; } = true;

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
            _services[typeof(T)] = new TransientServiceFactory(serviceProvider => factory(serviceProvider));
        }

        internal void AddSingletonService<T>(T singleton)
            where T : notnull
        {
            _services[typeof(T)] = new SingletonServiceFactory(singleton);
        }

        internal void AddSingletonService<TService, TImplementation>()
            where TImplementation: TService
        {
            _services[typeof(TService)] = new SingletonServiceFactory(typeof(TImplementation));
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
