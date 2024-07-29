using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Couchbase.Extensions.Metrics.Otel;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests
{
    public class InMemoryTracingFixture : BaseClusterFixture
    {
        private readonly TracerProvider _tracerProvider;

        public List<Activity> ExportedItems { get; set; }

        public InMemoryTracingFixture()
        {
            exportedItems = new List<Activity>();

            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService("couchbase-tests");

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddInMemoryExporter(exportedItems)
                .AddCouchbaseInstrumentation()
                .Build();
        }
        public override async Task<IBucket> GetDefaultBucket()
        {
            // Ensure that any tracing from other tests is complete before proceeding,
            // otherwise spans may leak into our exportedItems from other tests.
            await Task.Delay(1000);

            return await base.GetDefaultBucket();
        }

        public override async Task DisposeAsync()
        {
            await base.DisposeAsync();

            _tracerProvider.Dispose();
        }
    }
}
