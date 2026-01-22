using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.OpenTelemetry.IntegrationTests;

// ReSharper disable once ClassNeverInstantiated.Global
public class InMemoryTracingFixture : BaseClusterFixture
{
    private readonly TracerProvider _tracerProvider;

    public InMemoryTracingFixture()
    {
        ExportedItems = new List<Activity>();

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOnSampler())
            .AddInMemoryExporter(ExportedItems)
            .AddCouchbaseInstrumentation()
            .Build();
    }

    protected override async Task<IBucket> GetDefaultBucket()
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
