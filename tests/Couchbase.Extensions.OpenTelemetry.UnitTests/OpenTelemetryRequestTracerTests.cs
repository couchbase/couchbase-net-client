using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.Tracing.Otel.UnitTests
{
    public class OpenTelemetryRequestTracerTests
    {
        private static readonly ActivitySource MyActivitySource = new ActivitySource(
            "MyCompany.MyProduct.MyLibrary");

        public static void Main()
        {
            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource("MyCompany.MyProduct.MyLibrary")
                .AddConsoleExporter()
                .Build();

            using (var activity = MyActivitySource.StartActivity("SayHello"))
            {
                activity?.SetTag("foo", 1);
                activity?.SetTag("bar", "Hello, World!");
                activity?.SetTag("baz", new int[] { 1, 2, 3 });
            }
        }
    }
}
