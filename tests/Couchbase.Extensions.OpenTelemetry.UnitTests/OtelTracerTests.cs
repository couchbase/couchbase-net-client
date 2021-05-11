using Couchbase.Extensions.Tracing.Otel.Tracing;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.Tracing.Otel.UnitTests
{
    public class OtelTracerTests
    {
        public void TestOtel()
        {
            var builder = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddConsoleExporter();

            var tracer = new OpenTelemetryRequestTracer(builder);
        }
    }
}
