using System;
using System.Threading.Tasks;
using Couchbase.Compression.Snappier;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.CombinationTests.Fixtures
{
    public class CouchbaseFixture : IDisposable, IAsyncDisposable, IAsyncLifetime
    {
        private readonly ClusterOptions _options;
        private ICluster _cluster;
        private readonly object _syncObj = new();
        public readonly TestSettings _testSettings;

        public CouchbaseFixture()
        {
            _options = GetOptionsFromConfig();
            _testSettings = GetSettingsFromConfig();

            if (_testSettings.UseLogging)
            {
                IServiceCollection serviceCollection = new ServiceCollection();
                serviceCollection.AddLogging(builder => builder
                    .AddFilter(level => level >= LogLevel.Debug)
                );
                var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
                loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);

                _options.WithLogging(loggerFactory);
            }

            if (_testSettings.UseCompression)
            {
                _options.WithSnappierCompression();
            }
        }

        public ClusterOptions GetOptionsFromConfig() => new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .Build()
                .GetSection("couchbase")
                .Get<ClusterOptions>();

        public TestSettings GetSettingsFromConfig()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .Build()
                .GetSection("testSettings")
                .Get<TestSettings>();
        }

        public async Task BuildAsync()
        {
            _cluster ??= await Couchbase.Cluster.ConnectAsync(_options.ConnectionString!, _options);
        }

        public ICluster Cluster
        {
            get
            {
                lock (_syncObj)
                {
                    if (_cluster == null)
                    {
                        throw new InvalidOperationException(
                            "Cluster is null. Call CouchbaseFixture.Build() before calling CouchbaseFixture.");
                    }
                }

                return _cluster;
            }
        }

        public async Task<bool> FlushBucket(bool isAlreadyFlushed)
        {
            if (!isAlreadyFlushed)
            {
                await Cluster.Buckets.FlushBucketAsync("default").ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task<IBucket> GetDefaultBucket()
        {
            await BuildAsync();
            return await Cluster.BucketAsync("default");
        }

        public async Task<IScope> GetDefaultScope()
        {
            await BuildAsync();
            var bucket = await GetDefaultBucket().ConfigureAwait(false);
            return await bucket.ScopeAsync("_default").ConfigureAwait(false);
        }

        public async Task<ICouchbaseCollection> GetDefaultCollection()
        {
            await BuildAsync();
            var bucket = await Cluster.BucketAsync("default");
            return bucket.DefaultCollection();
        }

        public void Dispose()
        {
            _cluster?.Dispose();
        }

        public async Task InitializeAsync()
        {
            _cluster ??= await Couchbase.Cluster.ConnectAsync(_options.ConnectionString!, _options);
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            if (_cluster != null)
            {
                await _cluster.DisposeAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_cluster != null)
            {
                await _cluster.DisposeAsync();
            }
        }
    }
}
