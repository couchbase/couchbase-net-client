using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.IO.Operations;
using Couchbase.Extensions.Metrics.Otel;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using Xunit;

namespace Couchbase.Extensions.OpenTelemetry.UnitTests
{
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

            // Act

            MetricTracker.KeyValue.TrackOperation(OpCode.Get, TimeSpan.FromSeconds(1));

            // Give the exporter time
            await Task.Delay(100);

            // Assert

            IEnumerable<MetricPoint> Enumerate(Metric metric)
            {
                var enumerator = metric.GetMetricPoints().GetEnumerator();
                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                }
            }

            var duration = exportedItems
                .ToList()
                .Where(p => p.Name == "db.couchbase.operations")
                .SelectMany(Enumerate)
                .Last();

            Assert.Equal(1000000, duration.GetHistogramSum());

            var count = exportedItems
                .ToList()
                .Where(p => p.Name == "db.couchbase.operations.count")
                .SelectMany(Enumerate)
                .Last();

            Assert.Equal(1, count.GetSumLong());
        }
    }
}
