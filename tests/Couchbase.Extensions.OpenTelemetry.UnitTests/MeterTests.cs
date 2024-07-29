using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public async Task LegacyDisabled_LegacyIsNotExported()
        {
            // Arrange

            var exportedItems = new List<Metric>();

            using var tracerProvider = Sdk.CreateMeterProviderBuilder()
                .AddCouchbaseInstrumentation(options =>
                {
                    options.ExcludeLegacyMetrics = true;
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

            // Assert

            Assert.Empty(exportedItems
                .Where(p => p.Name == Names.OperationCounts)
                .SelectMany(Enumerate)
                .Where(p => IsOperation(p, Kv.SetUpsert)));
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
    }
}
