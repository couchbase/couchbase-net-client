using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.IntegrationTests.Fixtures
{
    public class ClusterFixture : IAsyncLifetime, IAsyncDisposable
    {
        private readonly TestSettings _settings;
        private bool _bucketOpened;

        public ClusterOptions ClusterOptions { get; }

        public ICluster Cluster { get; private set; }

        public ClusterFixture()
            : this(null)
        {
        }

        public ClusterFixture(Action<ClusterOptions> configureOptions)
        {
            _settings = GetSettings();

            var options = GetClusterOptions();
            configureOptions?.Invoke(options);
            ClusterOptions = options;
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

            return options;
        }

        public async Task InitializeAsync()
        {
            Cluster = await Couchbase.Cluster.ConnectAsync(
                    _settings.ConnectionString,
                    ClusterOptions)
                .ConfigureAwait(false);
        }

        public Task DisposeAsync()
        {
            if (Cluster != null)
            {
                var loggerFactory = Cluster.ClusterServices.GetRequiredService<ILoggerFactory>();

                Cluster.Dispose();
                Cluster = null;

                loggerFactory.Dispose();
            }

            return Task.CompletedTask;
        }

        ValueTask IAsyncDisposable.DisposeAsync() => new ValueTask(DisposeAsync());
    }
}
