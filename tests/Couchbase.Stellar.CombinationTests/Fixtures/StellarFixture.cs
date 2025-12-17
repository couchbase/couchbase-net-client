using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.Stellar.CombinationTests.Fixtures
{
    public class FixtureSettings
    {
        [JsonPropertyName("Scheme")]
        public string? Scheme { get; init; }

        [JsonPropertyName("Hostname")]
        public string? Hostname { get; init; }

        [JsonPropertyName("Username")]
        public string? Username { get; init; }

        [JsonPropertyName("Password")]
        public string? Password { get; init; }

        [JsonPropertyName("EnableDnsSrvResolution")]
        public bool EnableDnsSrvResolution { get; init; }

        [JsonPropertyName("Bucket")]
        public string Bucket { get; init; } = "default";
    }
    public class StellarFixture : IDisposable, IAsyncDisposable, IAsyncLifetime
    {
        private readonly ClusterOptions _options;
        private FixtureSettings _settings;
        private ICluster? _cluster;
        private ICluster? _stellarCluster;
        private readonly object _syncObj = new();

        public StellarFixture()
        {
            _settings = GetOptionsFromConfig();
            _options = new ClusterOptions
            {
                UserName = _settings.Username,
                Password = _settings.Password,
                ConnectionString = _settings.Scheme + "://" + _settings.Hostname,
                EnableDnsSrvResolution = _settings.EnableDnsSrvResolution,
                HttpIgnoreRemoteCertificateMismatch = true,
                KvIgnoreRemoteCertificateNameMismatch = true,
                KvCertificateCallbackValidation = (_, _, _, _) => true,
                HttpCertificateCallbackValidation = (_, _, _, _) => true
            };

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/Stellar-CombinationTests-{Date}.txt", LogLevel.Debug);

            _options.WithLogging(loggerFactory);
        }

        public FixtureSettings GetOptionsFromConfig() => new ConfigurationBuilder()
            .AddJsonFile("settings.json")
            .Build()
            .GetSection("couchbase")
            .Get<FixtureSettings>();

        public async Task BuildAsync()
        {
            _stellarCluster ??= await Couchbase.Cluster.ConnectAsync(_options.ConnectionString!, _options);
            _cluster ??= await Couchbase.Cluster.ConnectAsync("couchbase://" + _settings.Hostname, _options);
        }

        public ICluster StellarCluster
        {
            get
            {
                lock (_syncObj)
                {
                    if (_stellarCluster == null)
                    {
                        throw new InvalidOperationException(
                            "Cluster is null. Call CouchbaseFixture.Build() before calling CouchbaseFixture.");
                    }
                }

                return _stellarCluster;
            }
        }

        public ICluster CouchbaseCluster
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
                await CouchbaseCluster.Buckets.FlushBucketAsync(_settings.Bucket).ConfigureAwait(true);
                return true;
            }

            return false;
        }

        public async Task<IBucket> DefaultBucket()
        {
            await BuildAsync();
            return await StellarCluster.BucketAsync(_settings.Bucket);
        }

        public async Task<IScope> GetDefaultScope()
        {
            await BuildAsync();
            var bucket = await DefaultBucket().ConfigureAwait(true);
            return await bucket.ScopeAsync("_default").ConfigureAwait(true);
        }

        public async Task<ICouchbaseCollection> DefaultCollection()
        {
            await BuildAsync();
            var bucket = await DefaultBucket();
            return bucket.DefaultCollection();
        }

        public void Dispose()
        {
            _cluster?.Dispose();
            _stellarCluster?.Dispose();
        }

        public async Task InitializeAsync()
        {
            _stellarCluster ??= await Couchbase.Stellar.StellarCluster.ConnectAsync(_options);
            _cluster ??= await Couchbase.Cluster.ConnectAsync("couchbase://" + _settings.Hostname, _options);
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
