using System;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Couchbase.CombinationTests
{
    public class CouchbaseFixture : IDisposable, IAsyncDisposable
    {
        private readonly ClusterOptions _options;
        private ICluster _cluster;
        private readonly object _syncObj = new();

        public CouchbaseFixture()
        {
            _options = new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .Build()
                .GetSection("couchbase")
                .Get<ClusterOptions>();

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);

            _options.WithLogging(loggerFactory);
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

        public async Task<ICouchbaseCollection> GetDefaultCollection()
        {
            await BuildAsync();
            var bucket = await Cluster.BucketAsync("default");
            return bucket.DefaultCollection();
        }

        public void Dispose()
        {
            _cluster.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await _cluster.DisposeAsync();
        }
    }
}
