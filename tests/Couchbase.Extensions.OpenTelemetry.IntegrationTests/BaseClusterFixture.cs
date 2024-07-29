using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Couchbase.Compression.Snappier;
using Couchbase.Extensions.Metrics.Otel;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests
{
    public abstract class BaseClusterFixture : IAsyncLifetime
    {
        private readonly TestSettings _settings;
        private bool _bucketOpened;
        private readonly TracerProvider _tracerProvider;
        private readonly MeterProvider _meterProvider;

        public ClusterOptions ClusterOptions { get; }

        public ICluster Cluster { get; private set; }

        public List<Activity> exportedItems { get; set; }

        public BaseClusterFixture()
        {
            _settings = GetSettings();
            ClusterOptions = GetClusterOptions();
        }

        public async ValueTask<ICluster> GetCluster()
        {
            if (_bucketOpened)
            {
                return Cluster;
            }

            await GetDefaultBucket().ConfigureAwait(false);
            return Cluster;
        }

        public virtual async Task<IBucket> GetDefaultBucket()
        {
            var bucket = await Cluster.BucketAsync(_settings.BucketName).ConfigureAwait(false);

            _bucketOpened = true;

            return bucket;
        }

        public async Task<ICouchbaseCollection> GetDefaultCollectionAsync()
        {
            var bucket = await GetDefaultBucket().ConfigureAwait(false);
            return await bucket.DefaultCollectionAsync();
        }

        public static TestSettings GetSettings()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("testSettings")
                .Get<TestSettings>();
        }

        public static ClusterOptions GetClusterOptions()
        {
            var settings = GetSettings();
            var options = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("couchbase")
                .Get<ClusterOptions>();

            if (settings.EnableLogging)
            {
                IServiceCollection serviceCollection = new ServiceCollection();
                serviceCollection.AddLogging(builder => builder
                    .AddFilter(level => level >= LogLevel.Debug)
                );
                var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
                loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
                options.WithLogging(loggerFactory);
            }

            options.WithSnappyCompression();

            options.TracingOptions
                .WithEnabled(true)
                .WithTracer(new OpenTelemetryRequestTracer());

            return options;
        }

        public async Task InitializeAsync()
        {
            Cluster = await Couchbase.Cluster.ConnectAsync(
                    _settings.ConnectionString,
                    GetClusterOptions())
                .ConfigureAwait(false);
        }

        public virtual Task DisposeAsync()
        {
            Cluster?.Dispose();

            return Task.CompletedTask;
        }
    }
}
