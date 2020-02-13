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

// ReSharper disable UnusedMember.Global

#nullable enable

namespace Couchbase
{
    public sealed class ClusterOptions
    {
        internal ConnectionString? ConnectionStringValue { get; set; }

        public string? ConnectionString
        {
            get => ConnectionStringValue?.ToString();
            set => ConnectionStringValue = value != null ? Couchbase.ConnectionString.Parse(value) : null;
        }
        public ClusterOptions WithConnectionString(string connectionString)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

            return this;
        }

        public IList<string> Buckets { get; set; } = new List<string>();
        public ClusterOptions WithBuckets(params string[] bucketNames)
        {
            if (!bucketNames?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(bucketNames)} cannot be null or empty.");
            }

            //just the name of the bucket for now - later make and actual cluster
            Buckets = new List<string>(bucketNames);
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

        public ILoggerFactory? Logging { get; set; }
        public ClusterOptions WithLogging(ILoggerFactory? loggerFactory = null)
        {
            Logging = loggerFactory;

            return this;
        }

        /// <summary>
        /// Provide a custom <see cref="ITypeSerializer"/>.
        /// </summary>
        public ITypeSerializer? Serializer { get; set; }

        /// <summary>
        /// Provide a custom <see cref="ITypeSerializer"/>.
        /// </summary>
        /// <param name="serializer">Serializer to use.</param>
        /// <returns><see cref="ClusterOptions"/>.</returns>
        public ClusterOptions WithSerializer(ITypeSerializer serializer)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            return this;
        }

        /// <summary>
        /// Provide a custom <see cref="ITypeTranscoder"/>.
        /// </summary>
        public ITypeTranscoder? Transcoder { get; set; }

        /// <summary>
        /// Provide a custom <see cref="ITypeTranscoder"/>.
        /// </summary>
        /// <param name="transcoder">Transcoder to use.</param>
        /// <returns><see cref="ClusterOptions"/>.</returns>
        public ClusterOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));

            return this;
        }

        /// <summary>
        /// Provide a custom <see cref="IDnsResolver"/> for DNS SRV resolution.
        /// </summary>
        public IDnsResolver? DnsResolver { get; set; }

        /// <summary>
        /// Provide a custom <see cref="IDnsResolver"/> for DNS SRV resolution.
        /// </summary>
        /// <param name="dnsResolver">DNS resolver to use.</param>
        /// <returns><see cref="ClusterOptions"/>.</returns>
        public ClusterOptions WithDnsResolver(IDnsResolver dnsResolver)
        {
            DnsResolver = dnsResolver ?? throw new ArgumentNullException(nameof(dnsResolver));

            return this;
        }

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

        /// <summary>
        /// Minimum number of connections per key/value node.
        /// </summary>
        public int NumKvConnections { get; set; } = 2;

        /// <summary>
        /// Maximum number of connections per key/value node.
        /// </summary>
        public int MaxKvConnections { get; set; } = 5;

        /// <summary>
        /// Amount of time with no activity before a key/value connection is considered idle.
        /// </summary>
        public TimeSpan IdleKvConnectionTimeout { get; set; } = TimeSpan.FromMinutes(1);

        public int MaxHttpConnection { get; set; } = 0;

        [Obsolete("Not supported in .NET, uses system defaults.")]
        public TimeSpan IdleHttpConnectionTimeout { get; set; }

        public CircuitBreakerConfiguration CircuitBreakerConfiguration { get; set; } =
            CircuitBreakerConfiguration.Default;

        public bool EnableOperationDurationTracing { get; set; } = true;

        public RedactionLevel RedactionLevel { get; set; } = RedactionLevel.None;

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
        public bool EnableDnsSrvResolution { get; set; } = true;

        #region DI

        private readonly IDictionary<Type, IServiceFactory> _services = DefaultServices.GetDefaultServices();

        /// <summary>
        /// Build a <see cref="IServiceProvider"/> from the currently registered services.
        /// </summary>
        /// <returns>The new <see cref="IServiceProvider"/>.</returns>
        internal IServiceProvider BuildServiceProvider()
        {
            AddSingletonService(this);
            AddSingletonService(Logging ?? new NullLoggerFactory());

            if (Serializer != null)
            {
                AddSingletonService(Serializer);
            }

            if (Transcoder != null)
            {
                AddSingletonService(Transcoder);
            }

            if (DnsResolver != null)
            {
                AddSingletonService(DnsResolver);
            }

            return new CouchbaseServiceProvider(_services);
        }

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
