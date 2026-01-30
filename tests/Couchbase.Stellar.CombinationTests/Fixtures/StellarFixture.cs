using System;
using System.Diagnostics;
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

    // ReSharper disable once ClassNeverInstantiated.Global
    public class StellarFixture : IDisposable, IAsyncDisposable, IAsyncLifetime
    {
        private readonly ClusterOptions _options;
        private readonly FixtureSettings? _settings;
        private ICluster? _stellarCluster;
        private readonly object _syncObj = new();

        public StellarFixture()
        {
            _settings = GetOptionsFromConfig();
            Debug.Assert(_settings != null, nameof(_settings) + " != null");
#pragma warning disable CS0618 // Type or member is obsolete
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
#pragma warning restore CS0618 // Type or member is obsolete

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Debug)
            );
            var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/Stellar-CombinationTests-{Date}.txt", LogLevel.Debug);

            _options.WithLogging(loggerFactory);
        }

        private static FixtureSettings? GetOptionsFromConfig() => new ConfigurationBuilder()
            .AddJsonFile("settings.json")
            .Build()
            .GetSection("couchbase")
            .Get<FixtureSettings>();

        private async Task BuildAsync()
        {
            _stellarCluster ??= await Cluster.ConnectAsync(_options.ConnectionString!, _options);
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

        public async Task<IBucket> DefaultBucket()
        {
            if (_stellarCluster == null)
            {
                await BuildAsync();
            }

            Debug.Assert(_settings != null, nameof(_settings) + " != null");
            return await StellarCluster.BucketAsync(_settings.Bucket);
        }

        public async Task<IScope> GetDefaultScope()
        {
            await BuildAsync();
            var bucket = await DefaultBucket();
            return await bucket.ScopeAsync("_default");
        }

        public async Task<ICouchbaseCollection> DefaultCollection()
        {
            await BuildAsync();
            var bucket = await DefaultBucket();
            // ReSharper disable once MethodHasAsyncOverload
            return bucket.DefaultCollection();
        }

        public void Dispose()
        {
            _stellarCluster?.Dispose();
        }

        public async Task InitializeAsync()
        {
            _stellarCluster ??= await Couchbase.Stellar.StellarCluster.ConnectAsync(_options);
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            if (_stellarCluster != null)
            {
                await _stellarCluster.DisposeAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_stellarCluster != null)
            {
                await _stellarCluster.DisposeAsync();
            }
        }
    }
}
