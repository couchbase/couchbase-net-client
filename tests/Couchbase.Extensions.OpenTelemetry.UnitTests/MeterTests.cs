using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Extensions.Metrics.Otel;
using Couchbase.Test.Common;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;
using static Couchbase.Core.Diagnostics.Metrics.MetricTracker;
using static Couchbase.Core.Diagnostics.Tracing.OuterRequestSpans.ServiceSpan;

namespace Couchbase.Extensions.OpenTelemetry.UnitTests
{
    [Collection(NonParallelDefinition.Name)]
    public class MeterTests
    {
        [Fact]
        public async Task BasicMetric_IsExported()
        {
            // Arrange

            var exportedItems = new List<Metric>();

            using var tracerProvider = Sdk.CreateMeterProviderBuilder()
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exportedItems, options =>
                {
                    options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1;
                    options.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
                .Build();

            var operation = new Get<object>
            {
                BucketName = "bucket",
                CName = "_default",
                SName = "_default"
            };

            // Act

            MetricTracker.KeyValue.TrackOperation(operation, TimeSpan.FromSeconds(1), null);

            // Give the exporter time
            await Task.Delay(100);
            tracerProvider.ForceFlush();

            // Shut down tracer provider to prevent simultaneous access to the List<T>
            tracerProvider.Shutdown();
            await Task.Delay(100);

            // Assert

            var duration = exportedItems
                .Where(p => p.Name == Names.Operations)
                .SelectMany(Enumerate)
                .Last(p => IsOperation(p, Kv.Get));

            Assert.Equal(1_000_000, duration.GetHistogramSum());

            var count = exportedItems
                .Where(p => p.Name == Names.OperationCounts)
                .SelectMany(Enumerate)
                .Last(p => IsOperation(p, Kv.Get));

            Assert.Equal(1, count.GetSumLong());
        }

        [Fact]
        public async Task DropRedundantCounters_CountersNotExported()
        {
            // Arrange

            var exportedItems = new List<Metric>();

            using var tracerProvider = Sdk.CreateMeterProviderBuilder()
                .AddCouchbaseInstrumentation(options =>
                {
                    options.DropLegacyRedundantCounters = true;
                })
                .AddInMemoryExporter(exportedItems, options =>
                {
                    options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1;
                    options.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
                .Build();

            var operation = new Set<object>("bucket", "key")
            {
                CName = "_default",
                SName = "_default"
            };

            // Act

            MetricTracker.KeyValue.TrackOperation(operation, TimeSpan.FromSeconds(1), null);

            // Give the exporter time
            await Task.Delay(100);
            tracerProvider.ForceFlush();

            // Shut down tracer provider to prevent simultaneous access to the List<T>
            tracerProvider.Shutdown();
            await Task.Delay(100);

            // Assert - redundant counters should be dropped
            Assert.DoesNotContain(exportedItems, p => p.Name == Names.OperationCounts);
            Assert.DoesNotContain(exportedItems, p => p.Name == Names.OperationStatus);
            Assert.DoesNotContain(exportedItems, p => p.Name == Names.Timeouts);

            // But histogram should still be present
            Assert.Contains(exportedItems, p => p.Name == Names.Operations);
        }

        [Fact]
        public async Task ModernMeter_ExportsWithModernTags()
        {
            // Arrange

            var exportedItems = new List<Metric>();

            using var tracerProvider = Sdk.CreateMeterProviderBuilder()
                .AddCouchbaseInstrumentation(options =>
                {
                    options.SemanticConvention = ObservabilitySemanticConvention.Modern;
                })
                .AddInMemoryExporter(exportedItems, options =>
                {
                    options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1;
                    options.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
                .Build();

            var operation = new Get<object>
            {
                BucketName = "bucket",
                CName = "_default",
                SName = "_default"
            };

            // Act

            MetricTracker.KeyValue.TrackOperation(operation, TimeSpan.FromSeconds(1), null);

            // Give the exporter time
            await Task.Delay(100);
            tracerProvider.ForceFlush();

            // Shut down tracer provider to prevent simultaneous access to the List<T>
            tracerProvider.Shutdown();
            await Task.Delay(100);

            // Assert - should have modern metric name
            var duration = exportedItems
                .Where(p => p.Name == Names.ModernOperations)
                .SelectMany(Enumerate)
                .Last(p => HasTag(p, "db.operation.name", Kv.Get));

            Assert.Equal(1.0, duration.GetHistogramSum(), precision: 3);

            // Should have modern tag names
            Assert.True(HasTag(duration, "db.namespace", "bucket"));
            Assert.True(HasTag(duration, "couchbase.scope.name", "_default"));
            Assert.True(HasTag(duration, "couchbase.collection.name", "_default"));

            // Should NOT have legacy metrics
            Assert.DoesNotContain(exportedItems, p => p.Name == Names.Operations);
        }

        [Fact]
        public async Task ModernMeter_DoesNotExportRedundantCounters()
        {
            // Arrange

            var exportedItems = new List<Metric>();

            using var tracerProvider = Sdk.CreateMeterProviderBuilder()
                .AddCouchbaseInstrumentation(options =>
                {
                    options.SemanticConvention = ObservabilitySemanticConvention.Modern;
                })
                .AddInMemoryExporter(exportedItems, options =>
                {
                    options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1;
                    options.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
                .Build();

            var operation = new Get<object>
            {
                BucketName = "bucket",
                CName = "_default",
                SName = "_default"
            };

            // Act

            MetricTracker.KeyValue.TrackOperation(operation, TimeSpan.FromSeconds(1), null);

            // Give the exporter time
            await Task.Delay(100);
            tracerProvider.ForceFlush();

            // Shut down tracer provider to prevent simultaneous access to the List<T>
            tracerProvider.Shutdown();
            await Task.Delay(100);

            // Assert - modern meter should NOT export redundant counters
            Assert.DoesNotContain(exportedItems, p => p.Name == Names.OperationCounts);
            Assert.DoesNotContain(exportedItems, p => p.Name == Names.OperationStatus);
            Assert.DoesNotContain(exportedItems, p => p.Name == Names.Timeouts);

            // But histogram should still be present
            Assert.Contains(exportedItems, p => p.Name == Names.ModernOperations);
        }

        [Fact]
        public async Task BothMeters_ExportsBothLegacyAndModern()
        {
            // Arrange

            var exportedItems = new List<Metric>();

            using var tracerProvider = Sdk.CreateMeterProviderBuilder()
                .AddCouchbaseInstrumentation(options =>
                {
                    options.SemanticConvention = ObservabilitySemanticConvention.Both;
                })
                .AddInMemoryExporter(exportedItems, options =>
                {
                    options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1;
                    options.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                })
                .Build();

            var operation = new Get<object>
            {
                BucketName = "bucket",
                CName = "_default",
                SName = "_default"
            };

            // Act

            MetricTracker.KeyValue.TrackOperation(operation, TimeSpan.FromSeconds(1), null);

            // Give the exporter time
            await Task.Delay(100);
            tracerProvider.ForceFlush();

            // Shut down tracer provider to prevent simultaneous access to the List<T>
            tracerProvider.Shutdown();
            await Task.Delay(100);

            // Assert - should have both legacy and modern histograms
            Assert.Contains(exportedItems, p => p.Name == Names.Operations);
            Assert.Contains(exportedItems, p => p.Name == Names.ModernOperations);

            // Should have operation counts with legacy tags only (modern meter does not emit redundant counters)
            var countsWithLegacyTags = exportedItems
                .Where(p => p.Name == Names.OperationCounts)
                .SelectMany(Enumerate)
                .Any(p => IsOperation(p, Kv.Get));

            Assert.True(countsWithLegacyTags, "Should have operation counts with legacy tags");
        }

        private static IEnumerable<MetricPoint> Enumerate(Metric metric)
        {
            var enumerator = metric.GetMetricPoints().GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }

        private static bool IsOperation(MetricPoint metricPoint, string operation)
        {
            foreach (var tag in metricPoint.Tags)
            {
                if (tag.Key == OuterRequestSpans.Attributes.Operation
                    && tag.Value is string valueString
                    && valueString == operation)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTag(MetricPoint metricPoint, string key, string expectedValue)
        {
            foreach (var tag in metricPoint.Tags)
            {
                if (tag.Key == key && tag.Value is string valueString && valueString == expectedValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
