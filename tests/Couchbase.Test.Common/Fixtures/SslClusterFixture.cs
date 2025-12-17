using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.IntegrationTests.Fixtures
{
    public class SslClusterFixture : IAsyncLifetime
    {
        private readonly TestSettings _settings;
        private bool _bucketOpened;
        private ILogger _logger;
        private ILoggerFactory _loggerFactory;

        public ICluster Cluster { get; private set; }

        public SslClusterFixture()
        {
            _settings = GetSettings();

            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder
                    .AddFilter(level => level >= LogLevel.Trace) // GetSettings().LogLevel ?
            );

            _loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
            _loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
            _logger = _loggerFactory.CreateLogger<SslClusterFixture>();
        }

        public string BucketName => _settings.BucketName;


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
                .Get<ClusterOptions>().WithLogging(_loggerFactory);
        }

        public async Task InitializeAsync()
        {
        }

        public Task DisposeAsync()
        {
            Cluster?.Dispose();

            return Task.CompletedTask;
        }

        public void Log(string? message, params object?[] args)
        {
            _logger.LogInformation( message, args);
        }
    }
}
