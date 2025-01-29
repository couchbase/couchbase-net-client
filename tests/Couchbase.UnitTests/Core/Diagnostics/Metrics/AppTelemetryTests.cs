#nullable enable
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Diagnostics.Metrics;

public class AppTelemetryTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private string agent = $"sdk/couchbase-net-client v{Assembly.GetAssembly(typeof(Cluster))?.GetName().Version} OS: {RuntimeInformation.OSDescription} Framework: {RuntimeInformation.FrameworkDescription} Architecture: {RuntimeInformation.ProcessArchitecture} CPU Cores: {Environment.ProcessorCount}";

    public AppTelemetryTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _testOutputHelper.WriteLine(agent);
    }

    [Fact]
    public void Test_Counter_Increment_Only()
    {
        var collector = new AppTelemetryCollector();
        collector.Enable();
        var node = "node1.example.com";
        var nodeUuid = "abc123";
        string? alternateNode = null;

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(150), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.TimedOut, AppTelemetryRequestType.Query);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(150), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.TimedOut, AppTelemetryRequestType.Query);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(150), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.TimedOut, AppTelemetryRequestType.Query);

        Assert.True(collector.TryExportMetricsAndReset(out var result));

        _testOutputHelper.WriteLine(result);

        Assert.Contains($"sdk_query_r_total{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
    }

    [Fact]
    public void Test_Histogram_And_Counter_Export()
    {
        var collector = new AppTelemetryCollector();
        collector.Enable();
        var node = "node1.example.com";
        var nodeUuid = "abc123";
        var bucket = "travel-sample";
        string? alternateNode = null;

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(1), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(5), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(3_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(150), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(5000), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(20000), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(3_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.TimedOut, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(3_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.TimedOut, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(3_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Canceled, AppTelemetryRequestType.KvRetrieval, bucket);

        Assert.True(collector.TryExportMetricsAndReset(out var result));

        _testOutputHelper.WriteLine(result);

        // KV Histograms
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"1\",agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 1", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"10\",agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 2", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"100\",agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"500\",agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"1000\",agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"2500\",agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"+Inf\",agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 4", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_sum{{agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3056.000", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_count{{agent=\"{agent}\",bucket=\"travel-sample\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 4", result);

        // KV Counters
        Assert.Contains($"sdk_kv_r_timedout{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\",bucket=\"travel-sample\"}} 2", result);
        Assert.Contains($"sdk_kv_r_canceled{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\",bucket=\"travel-sample\"}} 1", result);
        Assert.Contains($"sdk_kv_r_total{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\",bucket=\"travel-sample\"}} 7", result);

        // Query Histograms
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"100\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 0", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"1000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 1", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"10000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 2", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"30000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"75000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"+Inf\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_query_duration_milliseconds_sum{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 25150.000", result);
        Assert.Contains($"sdk_query_duration_milliseconds_count{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
    }

    [Fact]
    public void Test_Service_Metrics_Export()
    {
        var collector = new AppTelemetryCollector();
        collector.Enable();
        var node = "node1.example.com";
        var nodeUuid = "abc123";
        string? alternateNode = null;

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(500), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(15000), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.Total, AppTelemetryRequestType.Query);

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(200), node, alternateNode, nodeUuid, AppTelemetryServiceType.Analytics, AppTelemetryCounterType.Total, AppTelemetryRequestType.Analytics);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(25000), node, alternateNode, nodeUuid, AppTelemetryServiceType.Analytics, AppTelemetryCounterType.Total, AppTelemetryRequestType.Analytics);

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(10_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.Query, AppTelemetryCounterType.TimedOut, AppTelemetryRequestType.Query);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(10_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.Analytics, AppTelemetryCounterType.Canceled, AppTelemetryRequestType.Analytics);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(10_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.Analytics, AppTelemetryCounterType.Total, AppTelemetryRequestType.Analytics);

        Assert.True(collector.TryExportMetricsAndReset(out var result));
        _testOutputHelper.WriteLine(result);

        // Service Counters
        Assert.Contains($"sdk_query_r_timedout{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 1", result);
        Assert.Contains($"sdk_query_r_canceled{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 0", result);
        Assert.Contains($"sdk_query_r_total{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 4", result);
        Assert.Contains($"sdk_analytics_r_timedout{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 0", result);
        Assert.Contains($"sdk_analytics_r_canceled{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 1", result);
        Assert.Contains($"sdk_analytics_r_total{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 4", result);

        // Query Histograms
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"100\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 1", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"1000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 2", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"10000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 2", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"30000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"75000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_query_duration_milliseconds_bucket{{le=\"+Inf\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_query_duration_milliseconds_sum{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 15550.000", result);
        Assert.Contains($"sdk_query_duration_milliseconds_count{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);

        // Analytics Histograms
        Assert.Contains($"sdk_analytics_duration_milliseconds_bucket{{le=\"100\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 0", result);
        Assert.Contains($"sdk_analytics_duration_milliseconds_bucket{{le=\"1000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 1", result);
        Assert.Contains($"sdk_analytics_duration_milliseconds_bucket{{le=\"10000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 2", result);
        Assert.Contains($"sdk_analytics_duration_milliseconds_bucket{{le=\"30000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_analytics_duration_milliseconds_bucket{{le=\"75000\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_analytics_duration_milliseconds_bucket{{le=\"+Inf\",agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_analytics_duration_milliseconds_sum{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 35200.000", result);
        Assert.Contains($"sdk_analytics_duration_milliseconds_count{{agent=\"{agent}\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
    }

    [Fact]
    public void When_Disabled_Should_Not_Collect_Metrics()
    {
        var collector = new AppTelemetryCollector();
        var node = "node1.example.com";
        var nodeUuid = "abc123";
        string? alternateNode = null;

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.TimedOut, AppTelemetryRequestType.KvRetrieval);

        Assert.False(collector.TryExportMetricsAndReset(out var result));
        Assert.Empty(result);
    }

    [Fact]
    public void Enable_Disable_Should_Clear_Metrics()
    {
        // Arrange
        var collector = new AppTelemetryCollector();
        collector.Enable();
        var node = "node1.example.com";
        var nodeUuid = "abc123";
        string? alternateNode = null;

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval);
        collector.Disable();

        Assert.False(collector.TryExportMetricsAndReset(out var result));
        Assert.Empty(result);
        Assert.Empty(collector.MetricSets);

        collector.Enable();
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval);

        Assert.True(collector.TryExportMetricsAndReset(out result));
        Assert.NotEmpty(result);
    }

    [Fact]
    public void IncrementMetrics_Should_Update_Both_Histogram_And_Counter()
    {
        var collector = new AppTelemetryCollector();
        collector.Enable();
        var node = "node1.example.com";
        var nodeUuid = "abc123";
        var bucket = "default";
        string? alternateNode = null;

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50),
            node,
            alternateNode,
            nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);

        Assert.True(collector.TryExportMetricsAndReset(out var result));

        Assert.Contains("sdk_kv_retrieval_duration_milliseconds_count", result);
        Assert.Contains("sdk_kv_r_total", result);
    }

    [Fact]
    public void Operations_Should_Be_Placed_In_Correct_Duration_Buckets()
    {
        var collector = new AppTelemetryCollector();
        collector.Enable();
        var node = "node1.example.com";
        var nodeUuid = "abc123";
        var bucket = "default";
        string? alternateNode = null;

        collector.IncrementMetrics(TimeSpan.FromMilliseconds(1), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(2), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(50), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(400), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(800), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(2_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);
        collector.IncrementMetrics(TimeSpan.FromMilliseconds(3_000), node, alternateNode, nodeUuid, AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total, AppTelemetryRequestType.KvRetrieval, bucket);

        Assert.True(collector.TryExportMetricsAndReset(out var result));

        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"1\",agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 1", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"10\",agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 2", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"100\",agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 3", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"500\",agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 4", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"1000\",agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 5", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"2500\",agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 6", result);
        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_bucket{{le=\"+Inf\",agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 7", result);

        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_count{{agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 7", result);

        Assert.Contains($"sdk_kv_retrieval_duration_milliseconds_sum{{agent=\"{agent}\",bucket=\"default\",node=\"node1.example.com\",node_uuid=\"abc123\"}} 6253.000", result);
    }
}
