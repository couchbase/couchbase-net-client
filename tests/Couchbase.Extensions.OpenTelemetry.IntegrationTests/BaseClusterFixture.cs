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
using OpenTelemetry.Resources;
using Xunit;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests
{
    public abstract class BaseClusterFixture : IAsyncLifetime
    {
        private readonly TestSettings _settings;
        private bool _bucketOpened;

        public ClusterOptions ClusterOptions { get; }

        private ICluster Cluster { get; set; }

        public List<Activity> ExportedItems { get; set; }

        protected BaseClusterFixture()
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

            await GetDefaultBucket();
            return Cluster;
        }

        protected virtual async Task<IBucket> GetDefaultBucket()
        {
            var bucket = await Cluster.BucketAsync(_settings.BucketName);

            _bucketOpened = true;

            return bucket;
        }

        public async Task<ICouchbaseCollection> GetDefaultCollectionAsync()
        {
            var bucket = await GetDefaultBucket();
            return await bucket.DefaultCollectionAsync();
        }

        private static TestSettings GetSettings()
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
                ;
        }

        public virtual Task DisposeAsync()
        {
            Cluster?.Dispose();

            return Task.CompletedTask;
        }
    }
}
