#nullable enable
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Couchbase.Compression.Snappier;
using Couchbase.Core.IO.Serializers;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Couchbase.Test.Common.Fixtures;

public class ClusterFixture : IAsyncLifetime, IAsyncDisposable
{
    private readonly TestSettings _settings;
    private bool _bucketOpened;
    private static ILogger? _logger;

    public ClusterOptions ClusterOptions { get; }

    public ICluster? Cluster { get; private set; }

    internal ClusterFixture(Action<ClusterOptions>? configureOptions)
    {
        _settings = GetSettings();

        var options = GetClusterOptions();
        configureOptions?.Invoke(options);
        ClusterOptions = options;

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder => builder
            .AddFilter(level => level >= LogLevel.Debug));
        var loggerFactory = serviceCollection.BuildServiceProvider().GetService<ILoggerFactory>();
        loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
        _logger = (loggerFactory ?? throw new InvalidOperationException()).CreateLogger<ClusterFixture>();
    }

    public async ValueTask<ICluster> GetCluster()
    {
        if (_bucketOpened)
        {
            return Cluster ?? throw new InvalidOperationException();
        }

        await GetDefaultBucket().ConfigureAwait(false);
        return Cluster ?? throw new InvalidOperationException();
    }

    public async Task<IBucket> GetDefaultBucket()
    {
        Debug.Assert(Cluster != null, nameof(Cluster) + " != null");
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
            .Get<TestSettings>() ?? throw new InvalidOperationException();
    }

    public TestSettings GetCapellaSettings()
    {
        return new ConfigurationBuilder()
            .AddJsonFile("config.json")
            .Build()
            .GetSection("capellaSettings")
            .Get<TestSettings>() ?? throw new InvalidOperationException();
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

            var loggerFactory = serviceCollection.BuildServiceProvider()
                .GetService<ILoggerFactory>();
            loggerFactory.AddFile("Logs/myapp-{Date}.txt", LogLevel.Debug);
            Debug.Assert(options != null, nameof(options) + " != null");
            options.WithLogging(loggerFactory);
        }

        if (settings.SystemTextJson)
        {
            Debug.Assert(options != null, nameof(options) + " != null");
            options.WithSerializer(SystemTextJsonSerializer.Create());
        }

        if (settings.EnableCompression)
        {
            options.WithSnappyCompression();
        }

        return options ?? throw new InvalidOperationException();
    }

    public async Task InitializeAsync()
    {
        Cluster = await Couchbase.Cluster.ConnectAsync(
                _settings.ConnectionString,
                ClusterOptions)
            .ConfigureAwait(false);
    }

    public void Log(string? message, params object?[] args)
    {
        Debug.Assert(message != null, nameof(message) + " != null");
        // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
        (_logger ?? throw new InvalidOperationException()).LogInformation(message, args);
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
