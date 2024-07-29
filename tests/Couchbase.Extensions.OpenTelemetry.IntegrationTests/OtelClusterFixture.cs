using System.Threading.Tasks;
using Couchbase.Extensions.Metrics.Otel;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests
{
    public class OtelClusterFixture : BaseClusterFixture
    {
        private readonly TracerProvider _tracerProvider;
        private readonly MeterProvider _meterProvider;

        public OtelClusterFixture()
        {
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService("couchbase-tests");

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("couchbase-tests"))
                .SetSampler(new AlwaysOnSampler())
                .AddOtlpExporter(ConfigureOtlpExporter)
                .AddCouchbaseInstrumentation()
                .Build();

            _meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter(ConfigureOtlpExporter)
                .AddCouchbaseInstrumentation(options =>
                {
                    options.ExcludeLegacyMetrics = true;
                })
                .Build();
        }

        private static void ConfigureOtlpExporter(OtlpExporterOptions options)
        {
            options.Endpoint = new System.Uri("http://localhost:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();

            _tracerProvider.Dispose();
            _meterProvider.Dispose();
        }
    }
}
