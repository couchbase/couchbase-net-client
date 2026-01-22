#nullable enable
using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.Test.Common.Fixtures;

// ReSharper disable once ClassNeverInstantiated.Global
public class SslClusterFixture : IAsyncLifetime
{
    private readonly TestSettings? _settings;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ICluster? Cluster { get; }

    public SslClusterFixture()
    {
        _settings = GetSettings();

        IServiceCollection serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder
                .AddFilter(level => level >= LogLevel.Trace) // GetSettings().LogLevel ?
        );

        _loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>() ?? throw new InvalidOperationException();
        _loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
        _logger = _loggerFactory.CreateLogger<SslClusterFixture>();
    }

    public string BucketName => _settings!.BucketName;


    public string GetCertsFilePath()
    {
        return GetSettings()?.CertificatesFilePath ?? throw new InvalidOperationException();
    }

    private static TestSettings? GetSettings()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("config.json")
            .Build()
            .GetSection("testSettings")
            .Get<TestSettings>();
    }

    public ClusterOptions? GetClusterOptions()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("config.json")
            .Build()
            .GetSection("couchbaseSsl")
            .Get<ClusterOptions>()
            ?.WithLogging(_loggerFactory);
    }

    public Task InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public Task DisposeAsync()
    {
        Cluster?.Dispose();

        return Task.CompletedTask;
    }

    public void Log(string? message, params object?[] args)
    {
        _logger.LogInformation(message, args);
    }
}
