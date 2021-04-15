using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.Formatters;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// An <see cref="IMetricsOutputFormatter"/> for aggregating Couchbase service latency as JSON.
    /// </summary>
    internal class LoggingMeterOutputFormatter : IMetricsOutputFormatter
    {
        /// <inheritdoc />
        public async Task WriteAsync(Stream output, MetricsDataValueSource metricsData,
            CancellationToken cancellationToken = new())
        {
            foreach (var report in metricsData.Contexts)
            {
                var timerValueSource = report.Timers.FirstOrDefault();
                if (timerValueSource == null) continue;

                var timerValue = timerValueSource.ValueProvider.Value;
                var endpoint = report.Context.Split('|')[1];

                var service = new JObject(
                    new JProperty(endpoint, new JObject(
                        new JProperty("total_count", timerValue.Histogram.Count),
                        new JProperty("percentiles_us", new JObject(
                            new JProperty("75.0", timerValue.Histogram.Percentile75),
                            new JProperty("95.0", timerValue.Histogram.Percentile95),
                            new JProperty("98.0", timerValue.Histogram.Percentile98),
                            new JProperty("99.9", timerValue.Histogram.Percentile999),
                            new JProperty("100.00", timerValue.Histogram.Max))))));

                using var writer = new StreamWriter(output);
                await writer.WriteAsync(service.ToString());
                await writer.FlushAsync();
                output.Position = 0;
            }
        }

        public MetricsMediaTypeValue MediaType => new("application", "vnd.couchbase.metrics", "v1.0", "json");
        public MetricFields MetricFields { get; set; }
    }
}
