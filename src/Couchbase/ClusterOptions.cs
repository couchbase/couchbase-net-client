using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Compatibility;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Connections.Channels;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Client.Transactions.Config;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.IO.Authentication.Authenticators;
using Google.Rpc.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// ReSharper disable UnusedMember.Global

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// Options controlling the connection to the Couchbase cluster.
    /// </summary>
    public sealed class ClusterOptions
    {
        internal ConnectionString? ConnectionStringValue { get; set; }

        /// <summary>
        /// The connection string for the cluster.
        /// </summary>
        public string? ConnectionString
        {
            get => ConnectionStringValue?.ToString();
            set
            {
                ConnectionStringValue = value != null ? Couchbase.ConnectionString.Parse(value) : null;
                if (!string.IsNullOrWhiteSpace(ConnectionStringValue?.Username))
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    UserName = ConnectionStringValue!.Username;
#pragma warning restore CS0618 // Type or member is obsolete
                }

                if (ConnectionStringValue != null)
                {
                    if (ConnectionStringValue.TryGetParameter(CStringParams.KvTimeout, out TimeSpan kvTimeout))
                    {
                        KvTimeout = kvTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.AnalyticsTimeout, out TimeSpan analyticsTimeout))
                    {
                        AnalyticsTimeout = analyticsTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.ConfigIdleRedialTimeout, out TimeSpan configIdleRedialTimeout))
                    {
                        ConfigIdleRedialTimeout = configIdleRedialTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.ConfigPollFloorInterval, out TimeSpan configPollFloorInterval))
                    {
                        ConfigPollFloorInterval = configPollFloorInterval;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.ConfigPollInterval, out TimeSpan configPollInterval))
                    {
                        ConfigPollInterval = configPollInterval;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.EnableMutationTokens, out bool enableMutationTokens))
                    {
                        EnableMutationTokens = enableMutationTokens;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.EnableTcpKeepAlives, out bool enableTcpAlives))
                    {
                        EnableTcpKeepAlives = enableTcpAlives;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.EnableTls, out bool enableTls))
                    {
                        EnableTls = enableTls;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.ForceIpv4, out bool forceIp4))
                    {
                        ForceIPv4 = forceIp4;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.KvConnectTimeout, out TimeSpan kvConnectTimeout))
                    {
                        KvConnectTimeout = kvConnectTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.KvDurableTimeout, out TimeSpan kvDurableTimeout))
                    {
                        KvDurabilityTimeout = kvDurableTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.QueryTimeout, out TimeSpan queryTimeout))
                    {
                        QueryTimeout = queryTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.ManagementTimeout, out TimeSpan managementTimeout))
                    {
                        ManagementTimeout = managementTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.MaxHttpConnections, out int maxHttpConnections))
                    {
                        MaxHttpConnections = maxHttpConnections;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.NumKvConnections, out int numKvConnections))
                    {
                        NumKvConnections = numKvConnections;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.ViewTimeout, out TimeSpan viewTimeout))
                    {
                        ViewTimeout = viewTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.SearchTimeout, out TimeSpan searchTimeout))
                    {
                        SearchTimeout = searchTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.TcpKeepAliveTime, out TimeSpan tcpKeepAliveTime))
                    {
                        TcpKeepAliveTime = tcpKeepAliveTime;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.TcpKeepAliveInterval, out TimeSpan tcpKeepAliveInterval))
                    {
                        TcpKeepAliveInterval = tcpKeepAliveInterval;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.MaxKvConnections, out int maxKvConnections))
                    {
                        MaxKvConnections = maxKvConnections;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.Compression, out bool compression))
                    {
                        Compression = compression;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.CompressionMinSize, out int compressionMinSize))
                    {
                        CompressionMinSize = compressionMinSize;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.CompressionMinRatio, out float compressionMinRatio))
                    {
                        CompressionMinRatio = compressionMinRatio;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.NetworkResolution, out string networkResolution))
                    {
                        NetworkResolution = networkResolution;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.RandomSeedNodes, out bool randomizeSeedNodes))
                    {
                        ConnectionStringValue.RandomizeSeedHosts = randomizeSeedNodes;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.AppTelemetryEndpoint, out string appTelemetryEndpoint))
                    {
                        AppTelemetry.Endpoint = new Uri(appTelemetryEndpoint);
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.AppTelemetryBackoff, out TimeSpan appTelemetryBackoff))
                    {
                        AppTelemetry.Backoff = appTelemetryBackoff;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.AppTelemetryPingInterval, out TimeSpan appTelemetryPingInterval))
                    {
                        AppTelemetry.PingInterval = appTelemetryPingInterval;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.AppTelemetryPingTimeout, out TimeSpan appTelemetryPingTimeout))
                    {
                        AppTelemetry.PingTimeout = appTelemetryPingTimeout;
                    }
                    if (ConnectionStringValue.TryGetParameter(CStringParams.PreferredServerGroup, out string serverGroup))
                    {
                        PreferredServerGroup = serverGroup;
                    }
                    if (ConnectionStringValue.Scheme == Scheme.Couchbases)
                    {
                        EnableTls = true;
                    }


                }
            }
        }

        public bool TryGetRawParameter(string key, out object? value)
        {
            if (ConnectionStringValue != null)
            {
                if (ConnectionStringValue.TryGetParameter(key, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Applies pre-set configuration values based on a named configuration profile.  Values defined in the named profile are applied and overwrite existing values.
        /// Values <em>not</em> defined in the profile do not overwrite existing values.
        /// </summary>
        /// <param name="profileName">The name of the profile to apply. (e.g. "default" or "wan-development")</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions ApplyProfile(string profileName) => profileName switch
        {
            "default" => ApplyProfile(ConfigProfiles.PreDefined.Default),
            "wan-development" => ApplyProfile(ConfigProfiles.PreDefined.WanDevelopment),
            _ => throw new ArgumentOutOfRangeException(nameof(profileName), "No such profile defined"),
        };

        /// <summary>
        /// Applies pre-set configuration values based on a named configuration profile.  Values defined in the named profile are applied and overwrite existing values.
        /// Values <em>not</em> defined in the profile do not overwrite existing values.
        /// </summary>
        /// <param name="profile">The profile to apply.</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>        [InterfaceStability(Level.Volatile)]
        public ClusterOptions ApplyProfile(ConfigProfiles.ConfigProfile profile)
        {
            // values in the Config Profile override current values only if they are set.

            // By explicitly calling the generated Deconstruct method here, the compiler will error out if we add new members to ConfigProfile and forget to handle them here.
            profile.Deconstruct(
                out var kvConnectTimeout,
                out var kvTimeout,
                out var kvDurabilityTimeout,
                out var viewTimeout,
                out var queryTimeout,
                out var analyticsTimeout,
                out var searchTimeout,
                out var managementTimeout
                );

            this.KvConnectTimeout = kvConnectTimeout ?? this.KvConnectTimeout;
            this.KvTimeout = kvTimeout ?? this.KvTimeout;
            this.KvDurabilityTimeout = kvDurabilityTimeout ?? this.KvDurabilityTimeout;
            this.ViewTimeout = viewTimeout ?? this.ViewTimeout;
            this.QueryTimeout = queryTimeout ?? this.QueryTimeout;
            this.AnalyticsTimeout = analyticsTimeout ?? this.AnalyticsTimeout;
            this.SearchTimeout = searchTimeout ?? this.SearchTimeout;
            this.ManagementTimeout = managementTimeout ?? this.ManagementTimeout;
            return this;
        }

        /// <summary>
        /// Set the connection string for the cluster.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        public ClusterOptions WithConnectionString(string connectionString)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

            return this;
        }

        /// <summary>
        /// The buckets to be used in the cluster.
        /// </summary>
        public IList<string> Buckets { get; set; } = new List<string>();

        /// <summary>
        /// Set the buckets to be used in the cluster.
        /// </summary>
        /// <param name="bucketNames">The names of the buckets.</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        public ClusterOptions WithBuckets(params string[] bucketNames)
        {
            if (!bucketNames?.Any() ?? true)
            {
                throw new ArgumentException($"{nameof(bucketNames)} cannot be null or empty.");
            }

            //just the name of the bucket for now - later make and actual cluster
            Buckets = new List<string>(bucketNames!);
            return this;
        }

        /// <summary>
        /// Set credentials used for authentication.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        /// <remarks>
        /// DEPRECATED: Use <see cref="WithPasswordAuthentication"/> instead.
        /// </remarks>
        [Obsolete("Use WithPasswordAuthentication instead. This method will be removed in a future version.")]
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

        #region New Authenticator API

        /// <summary>
        /// Sets the authenticator to use for authentication.
        /// </summary>
        /// <param name="authenticator">The authenticator to use.</param>
        /// <returns>A reference to this <see cref="ClusterOptions"/> object for method chaining.</returns>
        public ClusterOptions WithAuthenticator(IAuthenticator authenticator)
        {
            Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
            return this;
        }

        /// <summary>
        /// Configures password authentication with username and password credentials.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>A reference to this <see cref="ClusterOptions"/> object for method chaining.</returns>
        public ClusterOptions WithPasswordAuthentication(string username, string password)
        {
            Authenticator = new PasswordAuthenticator(username, password, EffectiveEnableTls);
            return this;
        }

        /// <summary>
        /// Configures certificate-based authentication (mTLS) using the specified certificate factory.
        /// </summary>
        /// <param name="certificateFactory">The certificate factory to use for providing client certificates.</param>
        /// <returns>A reference to this <see cref="ClusterOptions"/> object for method chaining.</returns>
        public ClusterOptions WithCertificateAuthentication(ICertificateFactory certificateFactory)
        {
            Authenticator = new CertificateAuthenticator(certificateFactory);
            EnableTls = true;
            return this;
        }

        /// <summary>
        /// Configures JWT (JSON Web Token) authentication.
        /// Automatically enables TLS as this is required for JWT authentication.
        /// </summary>
        /// <param name="token">The JWT token to use for authentication.</param>
        /// <returns>A reference to this <see cref="ClusterOptions"/> object for method chaining.</returns>
        [InterfaceStability(Level.Uncommitted)]
        public ClusterOptions WithJwtAuthentication(string token)
        {
            Authenticator = new JwtAuthenticator(token);
            EnableTls = true;
            return this;
        }

        /// <summary>
        /// Configures TLS settings for server certificate validation.
        /// </summary>
        /// <param name="configure">Action to configure TLS settings.</param>
        /// <returns>A reference to this <see cref="ClusterOptions"/> object for method chaining.</returns>
        public ClusterOptions WithTlsSettings(Action<TlsSettings> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            var settings = new TlsSettings();
            configure(settings);
            TlsSettings = settings;
            return this;
        }

        /// <summary>
        /// Configures trusted server CA certificates for server certificate validation.
        /// </summary>
        /// <param name="certificates">The server CA certificates to trust.</param>
        /// <returns>A reference to this <see cref="ClusterOptions"/> object for method chaining.</returns>
        public ClusterOptions WithTrustedServerCertificates(X509Certificate2Collection certificates)
        {
            if (TlsSettings == null)
            {
                TlsSettings = new TlsSettings();
            }
            TlsSettings.TrustedServerCertificateFactory = new PredefinedCertificateFactory(certificates);
            return this;
        }

        /// <summary>
        /// Gets the effective authenticator, resolving backward compatibility with legacy properties.
        /// Internals should call this instead of directly getting the ClusterOptions.Authenticator property.
        /// </summary>
        /// <returns>The effective authenticator to use.</returns>
        internal IAuthenticator GetEffectiveAuthenticator()
        {
            if (Authenticator != null)
            {
                return Authenticator;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            if (X509CertificateFactory != null)
            {
                return Authenticator = new CertificateAuthenticator(X509CertificateFactory);
            }

            if (!string.IsNullOrEmpty(UserName))
            {
                return Authenticator = new PasswordAuthenticator(UserName!, Password ?? string.Empty, EffectiveEnableTls);
            }
#pragma warning restore CS0618 // Type or member is obsolete

            throw new InvalidConfigurationException(
                "No authentication method is configured. Please set an IAuthenticator using ClusterOptions.WithAuthenticator or related fluent methods.");
        }

        #endregion

        /// <summary>
        /// The <see cref="ILoggerFactory"/> to use for logging.
        /// </summary>
        public ILoggerFactory? Logging { get; set; }

        /// <summary>
        /// Set the <see cref="ILoggerFactory"/> to use for logging.
        /// </summary>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
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
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
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
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        public ClusterOptions WithTranscoder(ITypeTranscoder transcoder)
        {
            Transcoder = transcoder ?? throw new ArgumentNullException(nameof(transcoder));

            return this;
        }

        public TransactionsConfig TransactionsConfig { get; set; } = new();

        /// <summary>
        /// Provide a custom <see cref="IDnsResolver"/> for DNS SRV resolution.
        /// </summary>
        public IDnsResolver? DnsResolver { get; set; }

        /// <summary>
        /// Provide a custom <see cref="IDnsResolver"/> for DNS SRV resolution.
        /// </summary>
        /// <param name="dnsResolver">DNS resolver to use.</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        public ClusterOptions WithDnsResolver(IDnsResolver dnsResolver)
        {
            DnsResolver = dnsResolver ?? throw new ArgumentNullException(nameof(dnsResolver));

            return this;
        }

        /// <summary>
        /// Provide a custom <see cref="ICompressionAlgorithm"/> for key/value body compression.
        /// </summary>
        /// <param name="compressionAlgorithm">Compression algorithm to use.</param>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithCompressionAlgorithm(ICompressionAlgorithm compressionAlgorithm) =>
            this.AddClusterService(compressionAlgorithm);

        /// <summary>
        /// Provide a custom <see cref="ICompressionAlgorithm"/> for key/value body compression.
        /// </summary>
        /// <typeparam name="TImplementation">Compression algorithm to use.</typeparam>
        /// <returns>
        /// A reference to this <see cref="ClusterOptions"/> object for method chaining.
        /// </returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithCompressionAlgorithm<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TImplementation : class, ICompressionAlgorithm
        {
            _services[typeof(ICompressionAlgorithm)] = new SingletonServiceFactory(typeof(TImplementation));

            return this;
        }

        /// <summary>
        /// Sets a preferred server group to target for operations that support this feature.
        /// Currently, these are: GetAnyReplica, GetAllReplicas, LookupInAnyReplica, LookupInAllReplicas.
        ///
        /// Note that while Server Groups have been available for a long time,
        /// this SDK feature is only available using Couchbase Server 7.6.2 and later.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public string? PreferredServerGroup { get; set; }

        /// <summary>
        /// Sets the <see cref="PreferredServerGroup"/>.
        /// </summary>
        /// <param name="serverGroup">The server group to prioritise</param>
        /// <returns>A reference to this <see cref="ClusterOptions"/> object for method chaining.</returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithPreferredServerGroup(string serverGroup)
        {
            PreferredServerGroup = serverGroup;
            return this;
        }

        #region Tracing & Metrics

        /// <summary>
        /// Enables request tracing within the SDK.
        /// <remarks>The default is enabled and the <see cref="RequestTracer"/> is configured.</remarks>
        /// </summary>
        public TracingOptions TracingOptions { get; set; } = new();

        /// <summary>
        /// Enables request tracing within the SDK.
        /// <remarks>The default is enabled and the <see cref="RequestTracer"/> is configured.</remarks>
        /// </summary>
        /// <param name="options">A <see cref="TracingOptions"/> object for configuration.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithTracing(TracingOptions options)
        {
            TracingOptions = options;
            return this;
        }

        /// <summary>
        /// Enables request tracing within the SDK.
        /// <remarks>The default is enabled and the <see cref="RequestTracer"/> is configured.</remarks>
        /// </summary>
        /// <param name="configure">A <see cref="TracingOptions"/> lambda for configuration.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithTracing(Action<TracingOptions> configure)
        {
            var opts = new TracingOptions();
            configure(opts);
            TracingOptions = opts;
            return this;
        }

        /// <summary>
        /// Configures threshold logging for the SDK.
        /// </summary>
        /// <remarks>The default is enabled and <see cref="ThresholdTraceListener"/> class is used.</remarks>
        public ThresholdOptions ThresholdOptions { get; set; } = new();

        /// <summary>
        /// Configures threshold logging for the SDK.
        /// </summary>
        /// <remarks>The default is enabled and <see cref="ThresholdTraceListener"/> class is used.</remarks>
        /// <param name="options">The <see cref="ThresholdOptions"/> for configuration.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithThresholdTracing(ThresholdOptions options)
        {
            ThresholdOptions = options;
            return this;
        }

        /// <summary>
        /// Configures request tracing for the SDK.
        /// </summary>
        /// <remarks>The default is enabled and <see cref="ThresholdTraceListener"/> class is used.</remarks>
        /// <param name="configure">The <see cref="Action{ThresholdOptions}"/> lambda to be configured.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithThresholdTracing(Action<ThresholdOptions> configure)
        {
            var opts = new ThresholdOptions();
            configure(opts);
            return WithThresholdTracing(opts);
        }

        /// <summary>
        /// Configures orphan logging for the SDK. Requires that <see cref="TracingOptions"></see> is enabled (the default).
        /// </summary>
        /// <remarks>The default is enabled and <see cref="OrphanTraceListener"/> class is used.</remarks>
        public OrphanOptions OrphanTracingOptions { get; set; } = new();

        /// <summary>
        /// Configures orphan logging for the SDK. Requires that <see cref="TracingOptions"></see> is enabled (the default).
        /// </summary>
        /// <remarks>The default is enabled and <see cref="OrphanTraceListener"/> class is used.</remarks>
        /// <param name="options">The <see cref="OrphanOptions"/> object for configuration.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithOrphanTracing(OrphanOptions options)
        {
            OrphanTracingOptions = options;
            return this;
        }

        /// <summary>
        /// Configures orphan logging for the SDK. Requires that <see cref="TracingOptions"></see> is enabled (the default).
        /// </summary>
        /// <remarks>The default is enabled and <see cref="OrphanTraceListener"/> class is used.</remarks>
        /// <param name="configure">The <see cref="OrphanOptions"/> lambda for configuration.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithOrphanTracing(Action<OrphanOptions> configure)
        {
            var opts = new OrphanOptions();
            configure(opts);
            return WithOrphanTracing(opts);
        }

        /// <summary>
        /// Configures logging for measuring latencies of the various Couchbase Services.
        /// </summary>
        /// <remarks>The default is enabled using the <see cref="LoggingMeter"/> class.</remarks>
        public LoggingMeterOptions LoggingMeterOptions { get; set; } = new();

        /// <summary>
        /// Configures logging for measuring latencies of the various Couchbase Services.
        /// </summary>
        /// <remarks>The default is enabled using the <see cref="LoggingMeter"/> class.</remarks>
        /// <param name="options">An <see cref="LoggingMeterOptions"/> object for configuration.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithLoggingMeterOptions(LoggingMeterOptions options)
        {
            LoggingMeterOptions = options;
            return this;
        }

        /// <summary>
        /// Configures logging for measuring latencies of the various Couchbase Services.
        /// </summary>
        /// <remarks>The default is enabled using the <see cref="LoggingMeter"/> class.</remarks>
        /// <param name="configure">An <see cref="LoggingMeterOptions"/> lambda for configuration.</param>
        /// <returns>A <see cref="ClusterOptions"/> object for chaining.</returns>
        public ClusterOptions WithLoggingMeterOptions(Action<LoggingMeterOptions> configure)
        {
            var opts = new LoggingMeterOptions();
            configure(opts);
            return WithLoggingMeterOptions(opts);
        }

        /// <summary>
        /// Configures the endpoint where AppTelemetry data should be sent to.
        /// </summary>
        /// <returns>
        /// A <see cref="ClusterOptions"/> object for chaining.
        /// </returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithAppTelemetryEndpoint(Uri endpoint)
        {
            AppTelemetry.Endpoint = endpoint;
            AppTelemetry.Enabled = true;
            return this;
        }

        /// <summary>
        /// Configures the backoff for re-connecting to the AppTelemetry endpoint via WebSockets.
        /// </summary>
        /// <returns>
        /// A <see cref="ClusterOptions"/> object for chaining.
        /// </returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithAppTelemetryBackoff(TimeSpan backoff)
        {
            AppTelemetry.Backoff = backoff;
            AppTelemetry.Enabled = true;
            return this;
        }

        /// <summary>
        /// Enables/Disables AppTelemetry.
        /// </summary>
        /// <returns>
        /// A <see cref="ClusterOptions"/> object for chaining.
        /// </returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithAppTelemetryEnabled(bool enabled)
        {
            AppTelemetry.Enabled = enabled;
            return this;
        }

        /// <summary>
        /// Configures the duration between consecutive PING commands sent to the server.
        /// </summary>
        /// <returns>
        /// A <see cref="ClusterOptions"/> object for chaining.</returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithAppTelemetryPingInterval(TimeSpan interval)
        {
            AppTelemetry.PingInterval = interval;
            AppTelemetry.Enabled = true;
            return this;
        }

        /// <summary>
        /// Configures the maximum timeout for the server to respond to a PING.
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns>
        /// A <see cref="ClusterOptions"/> object for chaining.</returns>
        [InterfaceStability(Level.Volatile)]
        public ClusterOptions WithAppTelemetryPingTimeout(TimeSpan timeout)
        {
            AppTelemetry.PingTimeout = timeout;
            AppTelemetry.Enabled = true;
            return this;
        }

        #endregion

        /// <summary>
        /// The <see cref="IRetryStrategy"/> for operation retries. Applies to all services: K/V, Query, etc.
        /// </summary>
        /// <param name="retryStrategy">The custom <see cref="RetryStrategy"/>.</param>
        /// <returns></returns>
        public ClusterOptions WithRetryStrategy(IRetryStrategy retryStrategy)
        {
            RetryStrategy = retryStrategy;
            return this;
        }

        /// <summary>
        /// The <see cref="IRetryStrategy"/> for operation retries. Applies to all services: K/V, Query, etc.
        /// </summary>
        public IRetryStrategy? RetryStrategy { get; set; } = new BestEffortRetryStrategy();

        /// <summary>
        /// The authenticator to use for authenticating connections to Couchbase Server.
        /// This and the fluent methods <see cref="WithAuthenticator"/> are the preferred way to
        /// configure authentication in the SDK.
        /// </summary>
        internal IAuthenticator? Authenticator
        {
            get => Volatile.Read(ref field);
            set => Interlocked.Exchange(ref field, value);
        }

        /// <summary>
        /// TLS settings for server certificate validation.
        /// </summary>
        public TlsSettings TlsSettings { get; set; } = new ();

        /// <summary>
        /// The username for authentication.
        /// </summary>
        /// <remarks>
        /// DEPRECATED: Use <see cref="Authenticator"/> with <see cref="PasswordAuthenticator"/> instead.
        /// This property is maintained for backward compatibility.
        /// </remarks>
        [Obsolete("Use Authenticator with PasswordAuthenticator instead. This property will be removed in a future version.")]
        public string? UserName { get; set; }

        /// <summary>
        /// The password for authentication.
        /// </summary>
        /// <remarks>
        /// DEPRECATED: Use <see cref="Authenticator"/> with <see cref="PasswordAuthenticator"/> instead.
        /// This property is maintained for backward compatibility.
        /// </remarks>
        [Obsolete("Use Authenticator with PasswordAuthenticator instead. This property will be removed in a future version.")]
        public string? Password { get; set; }

        /// <summary>
        /// The time to wait for a bucket re-configuration to take place after receiving a new cluster map config.
        /// </summary>
        /// <remarks>The default is 15s.</remarks>
        public TimeSpan ConfigUpdatingTimeout { get; set; } = TimeSpan.FromSeconds(15);

        //Foundation RFC conformance
        /// <summary>
        /// The time to wait while attempting to connect to a node’s KV service via a socket. Initial connection, reconnecting, node added, etc.
        /// </summary>
        /// <remarks> The default is 10s.</remarks>
        public TimeSpan KvConnectTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The time to wait before timing out a KV operation by the client.
        /// </summary>
        /// <remarks>The default is 2.5s.</remarks>
        public TimeSpan KvTimeout { get; set; } = TimeSpan.FromSeconds(2.5);

        /// <summary>
        /// The time to wait before timing out a KV Range Scan.
        /// </summary>
        public TimeSpan KvScanTimeout { get; set; } = TimeSpan.FromSeconds(75);

        /// <summary>
        /// The time to wait before timing out a KV operation that is either using synchronous durability or observe-based durability.
        /// </summary>
        /// <remarks>The default is 10s.</remarks>
        public TimeSpan KvDurabilityTimeout { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The time to wait before timing out a View request by the client.
        /// </summary>
        /// <remarks>The default is 75s.</remarks>
        public TimeSpan ViewTimeout { get; set; } = TimeSpan.FromSeconds(75);

        /// <summary>
        /// The time to wait before timing out a Query or N1QL request by the client.
        /// </summary>
        /// <remarks>The default is 75s.</remarks>
        public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(75);

        /// <summary>
        /// The time to wait before timing out an Analytics request by the client.
        /// </summary>
        /// <remarks>The default is 75s.</remarks>
        public TimeSpan AnalyticsTimeout { get; set; } = TimeSpan.FromSeconds(75);

        /// <summary>
        /// Number of seconds to wait before timing out a Search request by the client.
        /// </summary>
        /// <remarks>The default is 75s.</remarks>
        public TimeSpan SearchTimeout { get; set; } = TimeSpan.FromSeconds(75);

        /// <summary>
        /// Number of seconds to wait before timing out a Management API request by the client.
        /// </summary>
        /// <remarks>The default is 75s.</remarks>
        public TimeSpan ManagementTimeout { get; set; } = TimeSpan.FromSeconds(75);

        /// <summary>
        /// Gets or sets the maximum number of operations that will be queued for processing per node.
        /// If this value is exceeded, any additional operations will be put into the retry loop.
        /// </summary>
        /// <remarks>Defaults to 1024 operations.</remarks>
        [InterfaceStability(Level.Volatile)]
        public uint KvSendQueueCapacity { get; set; } = 1024;

        /// <summary>
        /// Overrides the TLS behavior in <see cref="ConnectionString"/>, enabling or
        /// disabling TLS.
        /// </summary>
        ///<remarks>Disabled default.</remarks>
        /// <seealso cref="KvCertificateCallbackValidation"/>
        /// <seealso cref="HttpCertificateCallbackValidation"/>
        public bool? EnableTls { get; set; }

        /// <summary>
        /// Enables mutation tokens for read consistency in Query searches.
        /// </summary>
        ///<remarks>They are enabled by default.</remarks>
        public bool EnableMutationTokens { get; set; } = true;

        /// <summary>
        ///  The duration between two keepalive transmissions in idle condition.
        /// </summary>
        /// <remarks>The default is every 1m.</remarks>
        public TimeSpan TcpKeepAliveTime { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// The duration between two successive keepalive retransmissions, if acknowledgement to the previous keepalive transmission is not received.
        /// </summary>
        /// <remarks>The default is every 1s.</remarks>
        public TimeSpan TcpKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Forces the SDK use IPv4 over IPv6
        /// </summary>
        /// <remarks>Defaults to disabled.</remarks>
        public bool ForceIPv4 { get; set; }

        /// <summary>
        /// The time between querying the server for new cluster map revisions.
        /// </summary>
        /// <remarks>The default is 2.5s.</remarks>
        public TimeSpan ConfigPollInterval { get; set; } = TimeSpan.FromSeconds(2.5);

        /// <summary>
        /// Not currently used.
        /// </summary>
        public TimeSpan ConfigPollFloorInterval { get; set; } = TimeSpan.FromMilliseconds(50);

        /// <summary>
        /// Not currently used.
        /// </summary>
        public TimeSpan ConfigIdleRedialTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Minimum number of connections per key/value node.
        /// </summary>
        /// <remarks>The default is 2; use the smallest number possible for best performance.</remarks>
        public int NumKvConnections { get; set; } = 2;

        /// <summary>
        /// Maximum number of connections per key/value node.
        /// </summary>
        /// <remarks>The default is 5; use the smallest number possible for best performance.
        /// A higher number of socket connections will increase the amount resources used by
        /// the server and harm performance.</remarks>
        public int MaxKvConnections { get; set; } = 5;

        /// <summary>
        /// Amount of time with no activity before a key/value connection is considered idle.
        /// </summary>
        /// <remarks>The default is 1m.</remarks>
        public TimeSpan IdleKvConnectionTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets the maximum number of simultaneous TCP connections allowed to a single server.
        /// </summary>
        /// <remarks>The default is 0 which equates to the maximum value or Int32.Max.</remarks>
        public int MaxHttpConnections { get; set; } = 0;

        /// <summary>
        /// The maximum time an HTTP connection will remain idle before being considered reusable.
        /// </summary>
        /// <remarks>The default is 1s.</remarks>
        public TimeSpan IdleHttpConnectionTimeout { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets how long a connection can be in the pool to be considered reusable.
        /// </summary>
        /// <remarks>Default of zero equates to the SocketsHttpHandler's default of -1 for infinite.</remarks>
        public TimeSpan HttpConnectionLifetime { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// The configuration for tuning the circuit breaker; if the default is changed ensure that the change is properly measured and tested.
        /// </summary>
        public CircuitBreakerConfiguration? CircuitBreakerConfiguration { get; set; } =
            CircuitBreakerConfiguration.Default;

        /// <summary>
        /// If true the server duration for an operation will be enabled and the value collected per K/V operation.
        /// </summary>
        /// <remarks>The default is enabled.</remarks>
        public bool EnableOperationDurationTracing { get; set; } = true;

        /// <summary>
        /// The redaction level for log files.
        /// </summary>
        /// <remarks>The default is <see cref="Couchbase.Core.Logging.RedactionLevel.None"/></remarks>
        public RedactionLevel RedactionLevel { get; set; } = RedactionLevel.None;

        /// <summary>
        /// Port used for HTTP bootstrapping fallback if other bootstrap methods are not available. Do not change unless the Cochbase server default ports have be changed.
        /// </summary>
        /// <remarks>The default is 8091.</remarks>
        public int BootstrapHttpPort { get; set; } = 8091;

        /// <summary>
        /// Port used for TLS HTTP bootstrapping fallback if other bootstrap methods are not available. Do not change unless the Cochbase server default ports have be changed.
        /// </summary>
        public int BootstrapHttpPortTls { get; set; } = 18091;

        /// <summary>
        /// Used for checking that the SDK has bootstrapped and potentially retrying if not.
        /// </summary>
        /// <remarks>The default is 2.5s.</remarks>
        public TimeSpan BootstrapPollInterval { get; set; } = TimeSpan.FromSeconds(2.5);

        //Volatile or obsolete options
        public bool EnableExpect100Continue { get; set; }

        [Obsolete("This property is ignored; set the ClusterOptions.X509CertificateFactory property to a "
                  +" ICertificateFactory instance - Couchbase.Core.IO.Authentication.X509.CertificateStoreFactory for example.")]
        public bool EnableCertificateAuthentication { get; set; }

        /// <summary>
        /// A <see cref="System.Boolean"/> value that specifies whether the certificate revocation list is checked during authentication.
        /// </summary>
        public bool EnableCertificateRevocation
        {
            get => TlsSettings.EnableCertificateRevocation;
            set => TlsSettings.EnableCertificateRevocation = value;

        }

        /// <summary>
        /// Ignore CertificateNameMismatch and CertificateChainMismatch, since they happen together.
        /// </summary>
        [Obsolete("Use KvIgnoreRemoteCertificateNameMismatch and/or HttpIgnoreRemoteCertificateMismatch instead of this property.")]
        public bool IgnoreRemoteCertificateNameMismatch
        {
            get => KvIgnoreRemoteCertificateNameMismatch && HttpIgnoreRemoteCertificateMismatch;
            set => KvIgnoreRemoteCertificateNameMismatch = HttpIgnoreRemoteCertificateMismatch = value;
        }

        /// <summary>
        /// Polls the server for cluster map configuration revision changes. This should always be enabled unless debugging the SDK.
        /// </summary>
        /// <remarks>This is enabled by default.</remarks>
        public bool EnableConfigPolling { get; set; } = true;

        /// <summary>
        /// Enables TCP Keep Alives.
        /// </summary>
        /// <remarks>This is enabled by default.</remarks>
        public bool EnableTcpKeepAlives { get; set; } = true;

        /// <summary>
        /// When bootstrapping, checks first that the connection string is a DNS SRV lookup;
        /// this can cause slower bootstrap times if not needed and can be disabled if DNS SRV is not being used.
        /// </summary>
        /// <remarks>This is enabled by default.</remarks>
        public bool EnableDnsSrvResolution { get; set; } = true;

        /// <summary>
        /// Specifies the network resolution strategy to use for alternative network; used in some container
        /// environments where there maybe internal and external addresses for connecting.
        /// </summary>
        /// <remarks>The default is "Auto"; Alternative addresses will be used if available.</remarks>
        public string NetworkResolution { get; set; } = Couchbase.NetworkResolution.Auto;
        [CanBeNull] internal string? EffectiveNetworkResolution { get; set; }
        internal bool HasNetworkResolution => !string.IsNullOrWhiteSpace(EffectiveNetworkResolution);

        /// <summary>
        /// Enables compression for key/value operations.
        /// </summary>
        /// <remarks>
        /// The value is ignored if no compression algorithm is supplied via <see cref="WithCompressionAlgorithm"/>.
        /// Defaults to true.
        /// </remarks>
        public bool Compression { get; set; } = true;

        /// <summary>
        /// If compression is enabled, the minimum document size considered for compression (in bytes).
        /// Documents smaller than this size are always sent to the server uncompressed.
        /// </summary>
        /// <remarks>The default is 32.</remarks>
        public int CompressionMinSize { get; set; } = 32;

        /// <summary>
        /// If compression is enabled, the minimum compression ratio to accept when sending documents to the server.
        /// Documents which don't achieve this compression ratio are sent to the server uncompressed.
        /// </summary>
        /// <remarks>
        /// 1.0 means no compression was achieved. A value of 0.75 would result in documents which compress to at least
        /// 75% of their original size to be transmitted compressed. The default is 0.83 (83%).
        /// </remarks>
        public float CompressionMinRatio { get; set; } = 0.83f;

        /// <inheritdoc cref="TuningOptions"/>
        public TuningOptions Tuning { get; set; } = new();

        /// <inheritdoc cref="ExperimentalOptions"/>
        public ExperimentalOptions Experiments { get; set; } = new();

        public AppTelemetryOptions AppTelemetry { get; set; } = new();

        /// <summary>
        /// Provides a default implementation of <see cref="ClusterOptions"/>.
        /// </summary>
        public static ClusterOptions Default => new ClusterOptions();

        /// <summary>
        /// Effective value for TLS, should be used instead of <see cref="EnableTls"/> internally within the SDK.
        /// </summary>
        internal bool EffectiveEnableTls => EnableTls ?? ConnectionStringValue?.Scheme == Scheme.Couchbases;

        /// <summary>
        /// Ignore CertificateNameMismatch and CertificateChainMismatch for Key/Value operations, since they happen together.
        /// </summary>
        /// <remarks>
        /// Intended for development purposes only.
        /// Does <b>NOT</b> affect anything other than the name mismatch,
        /// such as an untrusted root or an expired certificate.
        /// </remarks>
        /// <seealso cref="KvCertificateCallbackValidation"/>
        /// <seealso cref="HttpIgnoreRemoteCertificateMismatch"/>
        [Obsolete("Use TlsSettings.KvIgnoreRemoteCertificateNameMismatch instead. This property will be removed in a future version.")]
        public bool KvIgnoreRemoteCertificateNameMismatch
        {
            get => TlsSettings.KvIgnoreRemoteCertificateNameMismatch;
            set => TlsSettings.KvIgnoreRemoteCertificateNameMismatch = value;
        }

        /// <summary>
        /// The default <see cref="RemoteCertificateValidationCallback"/> called by .NET to validate the TLS/SSL certificates being used for
        /// Key/Value operations. If this method returns <code>true</code>, then the certificate will be accepted.
        /// </summary>
        /// <remarks>
        /// Proper SSL/TLS certificate validation is a complex subject.
        /// While it can be handy to simply <code>return true</code> for development against self-signed certificates,
        /// such a shortcut should never be used against a public-facing or production system.
        /// </remarks>
        [Obsolete("Use TlsSettings.KvCertificateValidationCallback instead. This property will be removed in a future version.")]
        public RemoteCertificateValidationCallback? KvCertificateCallbackValidation
        {
            get => TlsSettings.KvCertificateValidationCallback;
            set => TlsSettings.KvCertificateValidationCallback = value;
        }

        /// <summary>
        /// Ignore CertificateNameMismatch and CertificateChainMismatch for HTTP services (Query, FTS, Analytics, etc), since they happen together.
        /// </summary>
        /// <remarks>
        /// Intended for development purposes only.
        /// Does <b>NOT</b> affect anything other than the name mismatch,
        /// such as an untrusted root or an expired certificate.
        /// </remarks>
        /// <seealso cref="KvIgnoreRemoteCertificateNameMismatch"/>
        /// <seealso cref="HttpCertificateCallbackValidation"/>
        [Obsolete("Use TlsSettings.HttpIgnoreRemoteCertificateNameMismatch instead. This property will be removed in a future version.")]
        public bool HttpIgnoreRemoteCertificateMismatch
        {
            get => TlsSettings.HttpIgnoreRemoteCertificateNameMismatch;
            set => TlsSettings.HttpIgnoreRemoteCertificateNameMismatch = value;
        }

        /// <summary>
        /// The default RemoteCertificateValidationCallback called by .NET to validate the TLS/SSL certificates being used for
        /// HTTP services (Query, FTS, Analytics, etc).
        ///  If this method returns <code>true</code>, then the certificate will be accepted.
        /// </summary>
        /// <remarks>
        /// Proper SSL/TLS certificate validation is a complex subject.
        /// While it can be handy to simply <code>return true</code> for development against self-signed certificates,
        /// such a shortcut should never be used against a public-facing or production system.
        /// </remarks>
        /// <seealso cref="KvCertificateCallbackValidation"/>
        [Obsolete("Use TlsSettings.HttpCertificateCallbackValidation instead. This property will be removed in a future version.")]
        public RemoteCertificateValidationCallback? HttpCertificateCallbackValidation
        {
            get => TlsSettings?.HttpCertificateValidationCallback;
            set => TlsSettings.HttpCertificateValidationCallback = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="ICertificateFactory"/> to provide client certificates during TLS authentication.
        /// </summary>
        /// <remarks>
        /// DEPRECATED: Use <see cref="Authenticator"/> with <see cref="CertificateAuthenticator"/> instead,
        /// and use <see cref="TlsSettings"/> for server certificate validation.
        /// This property is maintained for backward compatibility.
        /// </remarks>
        [Obsolete("Use Authenticator with CertificateAuthenticator for client certs and TlsSettings for server CAs. This property will be removed in a future version.")]
        public ICertificateFactory? X509CertificateFactory { get; set; }

        /// <summary>
        /// Use the given <see cref="ICertificateFactory"/> to provide client certificates during TLS authentication.
        /// </summary>
        /// <param name="certificateFactory">The certificate factory to use.</param>
        /// <returns>The ClusterOptions to continue configuration in a fluent style.</returns>
        /// <exception cref="NullReferenceException">The certificateFactory parameter cannot be null.</exception>
        /// <remarks>
        /// DEPRECATED: Use <see cref="WithCertificateAuthentication"/> instead.
        /// </remarks>
        [Obsolete("Use WithCertificateAuthentication instead. This method will be removed in a future version.")]
        public ClusterOptions WithX509CertificateFactory(ICertificateFactory certificateFactory)
        {
            X509CertificateFactory = certificateFactory ?? throw new NullReferenceException(nameof(certificateFactory));
            Authenticator = new CertificateAuthenticator(certificateFactory);
            EnableTls = true;
            return this;
        }

        /// <summary>
        /// Allows unordered execution of commands by the server.
        /// </summary>
        /// <remarks>The default is enabled.</remarks>
        public bool UnorderedExecutionEnabled { get; set; } = true;

        /// <summary>
        /// If <see cref="ForceIpAsTargetHost"/> is true, send the IP as the target host during TLS authentication. If <see cref="ForceIpAsTargetHost"/> is false,
        /// then mimic the default SDK2 behavior; the hostname or IP as defined by the server will be sent as the target host during TLS authentication.
        /// Note: Setting this to true will cause a certificate name mismatch when connecting to Capella, since the TLS certificate is tied to the hostname.
        /// </summary>
        /// <remarks>Only applies when <see cref="EnableTls"/> is true.</remarks>
        /// <remarks>The default is false and the Hostname will be sent as the target host.</remarks>
        public bool ForceIpAsTargetHost { get; set; } = false;

        /// <summary>
        /// Enabled SSL Protocols
        /// </summary>
        /// <remarks>The defaults TLS1.2 and TLS1.3 if supported. Earlier versions are considered insecure.</remarks>
        [Obsolete("Use TlsSettings.EnabledSslProtocols instead. This property will be removed in a future version.")]
        public SslProtocols EnabledSslProtocols
        {
            get => TlsSettings.EnabledSslProtocols;
            set => TlsSettings.EnabledSslProtocols = value;

        }

#if NETCOREAPP3_1_OR_GREATER
        /// <summary>
        /// List of enabled TLS Cipher Suites.  If not set, will use default .NET Cipher Suites
        /// </summary>
        [UnsupportedOSPlatform("windows")]
        public List<TlsCipherSuite>? EnabledTlsCipherSuites { get; set; }

        [UnsupportedOSPlatformGuard("windows")]
        internal readonly bool PlatformSupportsCipherSuite = !OperatingSystem.IsWindows();
#endif

        internal bool IsCapella => ConnectionStringValue?.Hosts?.Any(h => h.Host.ToLowerInvariant().EndsWith(".cloud.couchbase.com")) == true;
        #region DI

        private readonly IDictionary<Type, IServiceFactory> _services = DefaultServices.GetDefaultServices();

        /// <summary>
        /// Build a <see cref="IServiceProvider"/> from the currently registered services.
        /// </summary>
        /// <returns>The new <see cref="IServiceProvider"/>.</returns>
        internal ICouchbaseServiceProvider BuildServiceProvider()
        {
            this.AddClusterService(this);
            this.AddClusterService(Logging ??= new NullLoggerFactory());

#region Tracing & Metrics

            this.AddClusterService(LoggingMeterOptions);
            if (!_services.ContainsKey(typeof(IMeter)))
            {
                if (LoggingMeterOptions.EnabledValue)
                {
                    this.AddClusterService<IMeter, LoggingMeter>();
                }
                else
                {
                    this.AddClusterService<IMeter>(NoopMeter.Instance);
                }
            }

            //set the tracer to be used
            //If the tracer is not NoopRequestTracer, wrap it in order to add ClusterLabels if provided by the cluster configs
            this.AddClusterService(TracingOptions is { Enabled: true, RequestTracer: not NoopRequestTracer } ? new RequestTracerWrapper(TracingOptions.RequestTracer, null) : NoopRequestTracer.Instance);

            #endregion

#pragma warning disable CS0618 // Type or member is obsolete
            if (Experiments.ChannelConnectionPools)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                this.AddClusterService<IConnectionPoolFactory, ChannelConnectionPoolFactory>();
            }

            if (Serializer != null)
            {
                this.AddClusterService(Serializer);
            }

            if (Transcoder != null)
            {
                this.AddClusterService(Transcoder);
            }

            if (DnsResolver != null)
            {
                this.AddClusterService(DnsResolver);
            }

            if (CircuitBreakerConfiguration != null)
            {
                this.AddClusterService(CircuitBreakerConfiguration);
            }

            if (RetryStrategy != null)
            {
                this.AddClusterService(RetryStrategy);
            }

            return new CouchbaseServiceProvider(_services);
        }

        /// <summary>
        /// Register a service with the cluster's <see cref="ICluster.ClusterServices"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service which will be requested.</typeparam>
        /// <typeparam name="TImplementation">The type of the service implementation which is returned.</typeparam>
        /// <param name="factory">Factory which will create the service.</param>
        /// <param name="lifetime">Lifetime of the service.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public ClusterOptions AddService<TService, TImplementation>(
            Func<IServiceProvider, TImplementation> factory,
            ClusterServiceLifetime lifetime)
            where TImplementation : notnull, TService
        {
            _services[typeof(TService)] = lifetime switch
            {
                ClusterServiceLifetime.Transient => new TransientServiceFactory(serviceProvider => factory(serviceProvider)),
                ClusterServiceLifetime.Cluster => new SingletonServiceFactory(serviceProvider => factory(serviceProvider)),
                _ => throw new InvalidEnumArgumentException(nameof(lifetime), (int) lifetime,
                    typeof(ClusterServiceLifetime))
            };

            return this;
        }

        /// <summary>
        /// Register a service with the cluster's <see cref="ICluster.ClusterServices"/>.
        /// </summary>
        /// <typeparam name="TService">The type of the service which will be requested.</typeparam>
        /// <typeparam name="TImplementation">The type of the service implementation which is returned.</typeparam>
        /// <param name="lifetime">Lifetime of the service.</param>
        /// <returns>The <see cref="ClusterOptions"/>.</returns>
        public ClusterOptions AddService<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
            ClusterServiceLifetime lifetime)
            where TImplementation : TService
        {
            _services[typeof(TService)] = lifetime switch
            {
                ClusterServiceLifetime.Transient => new TransientServiceFactory(typeof(TImplementation)),
                ClusterServiceLifetime.Cluster => new SingletonServiceFactory(typeof(TImplementation)),
                _ => throw new InvalidEnumArgumentException(nameof(lifetime), (int) lifetime,
                    typeof(ClusterServiceLifetime))
            };

            return this;
        }

#endregion
    }

    [Obsolete("Use Couchbase.NetworkResolution")]
    public static class NetworkTypes
    {
        public const string Auto = "auto";
        public const string Default = "default";
        public const string External = "external";
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
