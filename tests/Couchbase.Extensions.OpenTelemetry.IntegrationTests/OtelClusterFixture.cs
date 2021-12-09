using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests
{
    public class OtelClusterFixture : IAsyncLifetime
    {
        private readonly TestSettings _settings;
        private bool _bucketOpened;
        private readonly TracerProvider _tracerProvider;

        public ClusterOptions ClusterOptions { get; }

        public ICluster Cluster { get; private set; }

        public List<Activity> exportedItems { get; set; }

        public OtelClusterFixture()
        {
            _settings = GetSettings();
            exportedItems = new List<Activity>();

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddInMemoryExporter(exportedItems)
                .AddCouchbaseInstrumentation()
                .Build();

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

        public async Task<IBucket> GetDefaultBucket()
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

        public Task DisposeAsync()
        {
            Cluster?.Dispose();
            _tracerProvider.Dispose();

            return Task.CompletedTask;
        }
    }
}
