using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Couchbase.IntegrationTests.Fixtures
{
    public class SslClusterFixture : IAsyncLifetime
    {
        private readonly TestSettings _settings;
        private bool _bucketOpened;

        public ICluster Cluster { get; private set; }

        public SslClusterFixture()
        {
            _settings = GetSettings();
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

        public string GetCertsFilePath()
        {
            return GetSettings().CertificatesFilePath;
        }

        internal static TestSettings GetSettings()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("testSettings")
                .Get<TestSettings>();
        }

        public ClusterOptions GetClusterOptions()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build()
                .GetSection("couchbaseSsl")
                .Get<ClusterOptions>();
        }

        public async Task InitializeAsync()
        {
            var clusterOptions = GetClusterOptions();
            Cluster = await NetClient.Cluster.ConnectAsync(
                    clusterOptions.ConnectionString,
                    clusterOptions)
                .ConfigureAwait(false);
        }

        public Task DisposeAsync()
        {
            Cluster?.Dispose();

            return Task.CompletedTask;
        }
    }
}
