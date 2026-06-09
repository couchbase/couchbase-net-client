#nullable enable
using System.Threading.Tasks;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.Grpc.Protocol.Transactions;
using Couchbase.KeyValue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.Exceptions;
using Couchbase.Extensions.Metrics.Otel;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using Couchbase.Grpc.Protocol.Observability;
using Couchbase.Grpc.Protocol.Sdk.CircuitBreaker;
using Google.Protobuf.Collections;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Attribute = Couchbase.Grpc.Protocol.Observability.Attribute;
using Serilog;
using Microsoft.Extensions.Logging;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Config;
using Couchbase.Core.Diagnostics;
using Couchbase.FitPerformer.Utils.Options;
using TransactionsConfig = Couchbase.Client.Transactions.Config.TransactionsConfig;

namespace Couchbase.FitPerformer.Utils
{
    public sealed class ClusterConnection
    {
        public ICluster Cluster { get; private set; }

        public string Hostname { get; init; }
        public string Username { get; init; }

        private static TracerProvider? _tracerProvider;
        private static MeterProvider? _meterProvider;
        private const int MaxConnectRetries = 3;
        private static readonly TimeSpan ConnectRetryInterval = TimeSpan.FromSeconds(5);

        public async Task<IBucket> GetBucketAsync(string name)
        {
            return await Cluster.BucketAsync(name).ConfigureAwait(false);
        }

        public async Task<ICouchbaseCollection> GetCollectionAsync(string bucketName, string scopeName, string collectionName)
        {
            var bucket = await Cluster.BucketAsync(bucketName).ConfigureAwait(false);
            var scope = await bucket.ScopeAsync(scopeName).ConfigureAwait(false);
            var collection = await scope.CollectionAsync(collectionName).ConfigureAwait(false);
            return collection;
        }

        public Task<ICouchbaseCollection> GetCollectionAsync(DocId docId) => GetCollectionAsync(docId.BucketName, docId.ScopeName, docId.CollectionName);

        public static async Task<ClusterConnection> CreateAsync(ClusterConnectionCreateRequest request, ILoggerFactory? loggerFactory, int retries = 0)
        {
            if (retries > MaxConnectRetries)
            {
                throw new TestFailureException($"Failed to connect to cluster in {retries} attempts.");
            }

            var clusterOptions = new Couchbase.ClusterOptions()
                {
                    RedactionLevel = Core.Logging.RedactionLevel.Partial
                }
                .WithConnectionString(request.ClusterHostname);

            ApplyAuthentication(clusterOptions, request);
            ApplyServerCertificate(clusterOptions, request);

            if (request.ClusterConfig != null) {
                // This is using gRPC and is being received correctly
                if (request.ClusterConfig.UseCustomSerializer)
                {
                    clusterOptions = clusterOptions.WithSerializer(new CustomSerializer());
                }

                if (request.ClusterConfig.ObservabilityConfig != null)
                {
                    ApplyObservabilityConfig(request.ClusterConfig.ObservabilityConfig, clusterOptions);
                    Serilog.Log.Information("Applied ObservabilityConfig");
                }

                ApplyAppTelemetryConfig(request.ClusterConfig, clusterOptions);

                if (request.ClusterConfig.HasPreferredServerGroup)
                {
                    clusterOptions.WithPreferredServerGroup(request.ClusterConfig.PreferredServerGroup);
                }

                if (request.ClusterConfig.Insecure)
                {
                    clusterOptions.HttpCertificateCallbackValidation = (_, _, _, _) => true;
                    clusterOptions.KvCertificateCallbackValidation = (_, _, _, _) => true;
                }

                if (request.ClusterConfig.CircuitBreakerConfig != null)
                {
                    var protoCbConfig = request.ClusterConfig.CircuitBreakerConfig;
                    var circuitBreakerConfig = new CircuitBreakerConfiguration();
                    if (protoCbConfig.Query != null)
                    {
                        //Since the .NET SDK uses 1 global CircuitBreakerConfiguration for all Nodes' CircuitBreaker to use,
                        //it can only accept 1 config from the driver and any subsequently processed one will overwrite the previous.
                        //If this changes in the future and the SDK handles 1 Config per Service, the following lines can be adapted
                        //to correctly apply the parsed config to each service.
                        if (protoCbConfig.Query != null) ApplyCircuitBreakerConfig(protoCbConfig.Query, circuitBreakerConfig);
                        if (protoCbConfig.Kv != null) ApplyCircuitBreakerConfig(protoCbConfig.Kv, circuitBreakerConfig);
                        if (protoCbConfig.Search != null) ApplyCircuitBreakerConfig(protoCbConfig.Search, circuitBreakerConfig);
                        if (protoCbConfig.Analytics != null) ApplyCircuitBreakerConfig(protoCbConfig.Analytics, circuitBreakerConfig);
                        if (protoCbConfig.Backup != null) ApplyCircuitBreakerConfig(protoCbConfig.Backup, circuitBreakerConfig);
                        if (protoCbConfig.Eventing != null) ApplyCircuitBreakerConfig(protoCbConfig.Eventing, circuitBreakerConfig);
                        if (protoCbConfig.Manager != null) ApplyCircuitBreakerConfig(protoCbConfig.Manager, circuitBreakerConfig);
                        if (protoCbConfig.View != null) ApplyCircuitBreakerConfig(protoCbConfig.View, circuitBreakerConfig);
                    }

                    clusterOptions.CircuitBreakerConfiguration = circuitBreakerConfig;
                }

                if (request.ClusterConfig.TransactionsConfig != null)
                {
                    var transactionConfig = new TransactionsConfig();
                    if (request.ClusterConfig.TransactionsConfig.HasDurability) transactionConfig.DurabilityLevel = DurabilityUtil.ConvertDurabilityLevel(request.ClusterConfig.TransactionsConfig.Durability);
                    if (request.ClusterConfig.TransactionsConfig.HasTimeoutMillis) transactionConfig.ExpirationTime = TimeSpan.FromMilliseconds(request.ClusterConfig.TransactionsConfig.TimeoutMillis);
                    if (request.ClusterConfig.TransactionsConfig.MetadataCollection != null)
                    {
                        transactionConfig.MetadataCollection = TxnOptionsUtil.ConvertCollectionToKeyspace(request.ClusterConfig.TransactionsConfig.MetadataCollection);
                    }

                    if (request.ClusterConfig.TransactionsConfig.CleanupConfig is not null)
                    {
                        var cleanupBuilder = TransactionCleanupConfigBuilder.Create();
                        cleanupBuilder.CleanupClientAttempts(request.ClusterConfig.TransactionsConfig.CleanupConfig.HasCleanupClientAttempts && request.ClusterConfig.TransactionsConfig.CleanupConfig.CleanupClientAttempts);
                        cleanupBuilder.CleanupLostAttempts(
                            request.ClusterConfig.TransactionsConfig.CleanupConfig.HasCleanupLostAttempts &&
                            request.ClusterConfig.TransactionsConfig.CleanupConfig.CleanupLostAttempts);
                        if (request.ClusterConfig.TransactionsConfig.CleanupConfig.HasCleanupWindowMillis)
                        {
                            cleanupBuilder.CleanupWindow(TimeSpan.FromMilliseconds(request.ClusterConfig.TransactionsConfig.CleanupConfig.CleanupWindowMillis));
                        }
                        // deal with the CleanupCollections
                        foreach( var coll in request.ClusterConfig.TransactionsConfig.CleanupConfig.CleanupCollection)
                        {
                            cleanupBuilder.AddCollection(TxnOptionsUtil.ConvertCollectionToKeyspace(coll));
                        }

                        transactionConfig.CleanupConfig = cleanupBuilder.Build();
                        clusterOptions.TransactionsConfig = transactionConfig;
                    }
                }
            }

            if (loggerFactory != null)
            {
                clusterOptions = clusterOptions.WithLogging(loggerFactory);
            }

            try
            {
                return new ClusterConnection
                {
                    Cluster = await Couchbase.Cluster.ConnectAsync(clusterOptions)
                        .ConfigureAwait(false),
                    Hostname = request.ClusterHostname,
                    Username = request.ClusterUsername
                };
            }
            catch (AuthenticationFailureException e)
            {
                // we can try again, after a delay.   Perhaps there are other Exceptions that
                // merit a retry here?   IDK, but this one for sure happens during some Rbac
                // Fit tests, and also some client tests.
                Log.Logger.Information($"Got {e.Message} attempting to connect to the cluster, retrying in {ConnectRetryInterval.Seconds} seconds...");
                await Task.Delay(ConnectRetryInterval).ConfigureAwait(false);
                return await CreateAsync(request, loggerFactory, ++retries).ConfigureAwait(false);
            }
        }

        private static void ApplyAppTelemetryConfig( Couchbase.Grpc.Protocol.Shared.ClusterConfig config,
            ClusterOptions clusterOptions)
        {
            if (config.HasAppTelemetryEndpoint)
            {
                clusterOptions.WithAppTelemetryEndpoint(new Uri(config.AppTelemetryEndpoint));
            }

            if (config.HasAppTelemetryBackoffSecs)
            {
                clusterOptions.WithAppTelemetryBackoff(TimeSpan.FromSeconds(config.AppTelemetryBackoffSecs));
            }

            if (config.HasAppTelemetryPingIntervalSecs)
            {
                clusterOptions.WithAppTelemetryPingInterval(TimeSpan.FromSeconds(config.AppTelemetryPingIntervalSecs));
            }

            if (config.HasAppTelemetryPingTimeoutSecs)
            {
                clusterOptions.WithAppTelemetryPingTimeout(TimeSpan.FromSeconds(config.AppTelemetryPingTimeoutSecs));
            }

            if (config.HasEnableAppTelemetry)
            {
                clusterOptions.WithAppTelemetryEnabled(config.EnableAppTelemetry);
            }
        }

        private static void ApplyServerCertificate(ClusterOptions clusterOptions, ClusterConnectionCreateRequest request)
        {
            if (request.ClusterConfig.HasCert && !string.IsNullOrWhiteSpace(request.ClusterConfig.Cert))
            {
                var serverCert = new X509Certificate2(
                    rawData: System.Text.Encoding.ASCII.GetBytes(request.ClusterConfig.Cert),
                    password: (string)null!);

                var serverCertCollection = new X509Certificate2Collection(serverCert);

                clusterOptions.WithTrustedServerCertificates(serverCertCollection);
                Serilog.Log.Information(
                    "Using new WithTrustedServerCertificates API to add a trusted server certificate to TlsSettings");



            }
        }

        private static void ApplyAuthentication(ClusterOptions clusterOptions, ClusterConnectionCreateRequest request)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (request.ClusterUsername != null) clusterOptions.UserName = request.ClusterUsername;
            if (request.ClusterPassword != null) clusterOptions.Password = request.ClusterPassword;
#pragma warning restore CS0618 // Type or member is obsolete

            if (request.Authenticator != null)
            {
                IAuthenticator authenticator = request.Authenticator.AuthenticatorCase switch
                {
                    Authenticator.AuthenticatorOneofCase.PasswordAuth =>
                        new PasswordAuthenticator(
                            request.Authenticator.PasswordAuth.Username,
                            request.Authenticator.PasswordAuth.Password),

                    Authenticator.AuthenticatorOneofCase.CertificateAuth => CreateCertificateAuthenticator(request.Authenticator.CertificateAuth),
                    Authenticator.AuthenticatorOneofCase.JwtAuth => new JwtAuthenticator(request.Authenticator.JwtAuth.Jwt),
                    Authenticator.AuthenticatorOneofCase.None => throw new NotSupportedException("No authenticator provided"),
                    _ => throw new NotSupportedException($"Unknown authenticator case: {request.Authenticator.AuthenticatorCase}")
                };

                clusterOptions.WithAuthenticator(authenticator);
                Serilog.Log.Information("Added an Authenticator of type {AuthenticatorType} to ClusterOptions", request.Authenticator.AuthenticatorCase);
            }
        }

        private static void ApplyObservabilityConfig(Grpc.Protocol.Observability.Config config, ClusterOptions clusterOptions)
        {
            if (config.ObservabilitySemanticConventionOptIn != null)
            {
                if (config.ObservabilitySemanticConventionOptIn.Contains(SemanticConvention.DatabaseDup))
                {
                    clusterOptions.ObservabilitySemanticConvention =
                        ObservabilitySemanticConvention.Both;
                } else if (config.ObservabilitySemanticConventionOptIn.Contains(SemanticConvention
                               .Database))
                {
                    clusterOptions.ObservabilitySemanticConvention =
                        ObservabilitySemanticConvention.Modern;
                }
                else
                {
                    clusterOptions.ObservabilitySemanticConvention =
                        ObservabilitySemanticConvention.Legacy;
                }
            }
            if (config.OrphanResponse != null)
            {
                var co = config.OrphanResponse;
                clusterOptions.WithOrphanTracing(options =>
                {
                    if (co.HasEnabled) options.Enabled = co.Enabled;
                    if (co.HasEmitIntervalMillis) options.EmitInterval = TimeSpan.FromMilliseconds(co.EmitIntervalMillis);
                    if (co.HasSampleSize) options.SampleSize = (uint)co.SampleSize;
                });
            }
            if (config.ThresholdLoggingTracer != null)
            {
                clusterOptions.WithThresholdTracing(ParseThresholdLoggingOptions(config.ThresholdLoggingTracer));
            }

            if (config.Tracing != null)
            {
                var ct = config.Tracing;
                CreateTracerProvider(ct, ParseResources(ct.Resources));

                var tracingOptions = new TracingOptions
                {
                    Enabled = true,
                    RequestTracer = new OpenTelemetryRequestTracer()
                };

                clusterOptions.WithTracing(tracingOptions);

            }
            if (config.Metrics != null)
            {
                var cm = config.Metrics;
                CreateMeterProvider(cm, ParseResources(cm.Resources),
                    clusterOptions);

                if (config.LoggingMeter != null)
                {
                    var cl = config.LoggingMeter;
                    clusterOptions.WithLoggingMeterOptions(options =>
                    {
                        if (cl.Enabled) options.Enabled(cl.Enabled);
                        if (cl.HasEmitIntervalMillis) options.EmitInterval(TimeSpan.FromMilliseconds(cl.EmitIntervalMillis));
                    });
                }
                else
                {
                    clusterOptions.WithLoggingMeterOptions(options =>
                    {
                        options.Enabled(true);
                        options.EmitInterval(TimeSpan.FromMilliseconds(cm.ExportEveryMillis));
                    });
                }
            }

            if (config.UseNoopTracer)
            {
                clusterOptions.WithTracing(new TracingOptions
                {
                    Enabled = true,
                    RequestTracer = new NoopRequestTracer()
                });
            }
        }

        private static void CreateMeterProvider(MetricsConfig config, ResourceBuilder resources, ClusterOptions clusterOptions)
        {
            _meterProvider = Sdk
                .CreateMeterProviderBuilder()
                .SetResourceBuilder(resources)
                .AddOtlpExporter((exporterOptions, readerOptions) =>
                {
                    exporterOptions.Endpoint = new System.Uri(config.EndpointHostname);
                    exporterOptions.Protocol = OtlpExportProtocol.Grpc;
                    readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = config.ExportEveryMillis;
                })
                .AddCouchbaseInstrumentation(options =>
                {
                    options.ExcludeLegacyMetrics = false;
                    options.SemanticConvention = clusterOptions.ObservabilitySemanticConvention;
                })
                .Build();
        }

        private static void CreateTracerProvider(TracingConfig config, ResourceBuilder resources)
        {
            var epsilon = 0.00001;
            _tracerProvider = Sdk
                .CreateTracerProviderBuilder()
                 // If the sampling percentage is:
                 // Too small => Always Off
                 // Too big => Always On
                 // Else => Probability-based
                .SetSampler(config.SamplingPercentage < epsilon
                    ? new AlwaysOffSampler()
                    : (config.SamplingPercentage > (1.0 - epsilon))
                        ? new AlwaysOnSampler()
                        : new TraceIdRatioBasedSampler(config.SamplingPercentage))
                .SetResourceBuilder(resources)
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new System.Uri(config.EndpointHostname);
                    options.ExportProcessorType =
                        config.Batching ? ExportProcessorType.Batch : ExportProcessorType.Simple;
                })
                .AddCouchbaseInstrumentation()
                .Build();
        }

        private static ThresholdOptions ParseThresholdLoggingOptions(ThresholdLoggingTracerConfig config)
        {
            var thresholdOptions = new ThresholdOptions();
            if (config.HasEnabled) thresholdOptions.Enabled = config.Enabled;
            if (config.HasSampleSize) thresholdOptions.WithSampleSize((uint)config.SampleSize);
            if (config.HasAnalyticsThresholdMillis) thresholdOptions.WithAnalyticsThreshold(TimeSpan.FromMilliseconds(config.AnalyticsThresholdMillis));
            if (config.HasEmitIntervalMillis) thresholdOptions.WithEmitInterval(TimeSpan.FromMilliseconds(config.EmitIntervalMillis));
            if (config.HasKvThresholdMillis) thresholdOptions.WithKvThreshold(TimeSpan.FromMilliseconds(config.KvThresholdMillis));
            if (config.HasQueryThresholdMillis) thresholdOptions.WithQueryThreshold(TimeSpan.FromMilliseconds(config.QueryThresholdMillis));
            if (config.HasSearchThresholdMillis) thresholdOptions.WithSearchThreshold(TimeSpan.FromMilliseconds(config.SearchThresholdMillis));
            if (config.HasTransactionsThresholdMillis) throw new NotSupportedException(".NET SDK does not support Transaction Threshold tracing.");
            if (config.HasViewsThresholdMillis) thresholdOptions.WithViewsThreshold(TimeSpan.FromMilliseconds(config.ViewsThresholdMillis));
            return thresholdOptions;
        }

        private static ResourceBuilder ParseResources(MapField<string, Attribute> resources)
        {
            return ResourceBuilder
                .CreateEmpty()
                .AddAttributes(resources.Select(kvp =>
                {
                    switch (kvp.Value.ValueCase)
                    {
                        case Attribute.ValueOneofCase.ValueBoolean:
                            return new KeyValuePair<string, object>(kvp.Key, kvp.Value.ValueBoolean);
                        case Attribute.ValueOneofCase.ValueLong:
                            return new KeyValuePair<string, object>(kvp.Key, kvp.Value.ValueLong);
                        case Attribute.ValueOneofCase.ValueString:
                            return new KeyValuePair<string, object>(kvp.Key, kvp.Value.ValueString);
                        case Attribute.ValueOneofCase.None:
                        default:
                            throw new InvalidArgumentException("Tracing Attribute is None.");
                    }
                }));
        }

        private static void ApplyCircuitBreakerConfig(ServiceConfig serviceConfig,
            CircuitBreakerConfiguration circuitBreakerConfig)
        {
            if (serviceConfig.HasEnabled) circuitBreakerConfig.Enabled = serviceConfig.Enabled;
            if (serviceConfig.HasVolumeThreshold) circuitBreakerConfig.VolumeThreshold = serviceConfig.VolumeThreshold;
            if (serviceConfig.HasCanaryTimeoutMs) circuitBreakerConfig.CanaryTimeout = TimeSpan.FromMilliseconds(serviceConfig.CanaryTimeoutMs);
            if (serviceConfig.HasErrorThresholdPercentage) circuitBreakerConfig.ErrorThresholdPercentage = (uint)serviceConfig.ErrorThresholdPercentage;
            if (serviceConfig.HasRollingWindowMs) circuitBreakerConfig.RollingWindow = TimeSpan.FromMilliseconds(serviceConfig.RollingWindowMs);
            if (serviceConfig.HasSleepWindowMs) circuitBreakerConfig.SleepWindow = TimeSpan.FromMilliseconds(serviceConfig.SleepWindowMs);
        }

        public async Task<ICouchbaseCollection> GetCollectionAsync(DocLocation loc) {
            Couchbase.Grpc.Protocol.Shared.Collection coll = null;

            switch (loc.LocationCase) {
                case DocLocation.LocationOneofCase.Pool:
                    coll = loc.Pool.Collection;
                    break;
                case DocLocation.LocationOneofCase.Specific:
                    coll = loc.Specific.Collection;
                    break;
                case DocLocation.LocationOneofCase.Uuid:
                    coll = loc.Uuid.Collection;
                    break;
                default:
                    throw new NotSupportedException();
            }

            return await GetCollectionAsync(coll.BucketName, coll.ScopeName, coll.CollectionName);
        }

        private static RemoteCertificateValidationCallback GetValidatorWithPredefinedCertificates(X509Certificate2Collection certs) =>
            (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) =>
            {
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;
                }

                if (chain == null)
                {
                    return false;
                }

#if NET5_0_OR_GREATER
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                foreach (var defaultCert in certs)
                {
                    chain.ChainPolicy.CustomTrustStore.Add(defaultCert);
                }
#endif
                if (certificate is X509Certificate2 cert2)
                {
                    chain.Reset();
                    var built = chain.Build(cert2);
                    return built;
                }
                return false;
            };

        internal static CertificateAuthenticator CreateCertificateAuthenticator( Couchbase.Grpc.Protocol.Shared.Authenticator.Types.CertificateAuthenticator protoCertAuth)
        {
            var cert = X509Certificate2.CreateFromPem(
                protoCertAuth.Cert,
                protoCertAuth.Key
            );

            return new CertificateAuthenticator(new PredefinedCertificateFactory(cert));
        }

        public void Dispose()
        {
            DisposeTelemetry();
            Cluster.Dispose();
        }
        public async Task DisposeAsync()
        {
            DisposeTelemetry();
            await Cluster.DisposeAsync().ConfigureAwait(false);
        }

        private void DisposeTelemetry()
        {
            _tracerProvider?.ForceFlush();
            _meterProvider?.ForceFlush();
            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
        }
    }
}