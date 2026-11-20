#if NETCOREAPP3_1_OR_GREATER
#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Stellar.Core;
using Couchbase.Test.Common;
using Xunit;

namespace Couchbase.UnitTests.Stellar.Core;

[Collection(NonParallelDefinition.Name)]
public class GrpcTraceContextPropagatorTests
{
    // Any named source marks an activity that a listener asked for; the real one is "System.Net.Http".
    private const string HttpSourceName = "Couchbase.UnitTests.FakeSystemNetHttp";
    private static readonly ActivitySource HttpSource = new(HttpSourceName);

    private static Dictionary<string, string?> Inject(Activity? activity)
    {
        var headers = new Dictionary<string, string?>();
        new GrpcTraceContextPropagator(DistributedContextPropagator.CreateDefaultPropagator())
            .Inject(activity, headers, static (carrier, key, value) => ((Dictionary<string, string?>)carrier!)[key] = value);
        return headers;
    }

    [Fact]
    public void Inject_SkipsFallbackHttpClientActivity_SendsGrpcSpan()
    {
        // HttpClient's propagation-only activity: no ActivitySource, child of the gRPC client span.
        var grpcSpan = new Activity("Grpc.Net.Client.GrpcOut").SetIdFormat(ActivityIdFormat.W3C).Start();
        var httpActivity = new Activity(GrpcTraceContextPropagator.HttpClientActivityName).Start();
        try
        {
            var headers = Inject(httpActivity);

            Assert.Equal(grpcSpan.Id, headers["traceparent"]);
        }
        finally
        {
            httpActivity.Stop();
            grpcSpan.Stop();
        }
    }

    [Fact]
    public void Inject_ListenedHttpClientActivity_IsSent()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == HttpSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);

        var grpcSpan = new Activity("Grpc.Net.Client.GrpcOut").SetIdFormat(ActivityIdFormat.W3C).Start();
        var httpActivity = HttpSource.StartActivity(GrpcTraceContextPropagator.HttpClientActivityName, ActivityKind.Client)!;
        try
        {
            var headers = Inject(httpActivity);

            Assert.Equal(httpActivity.Id, headers["traceparent"]);
        }
        finally
        {
            httpActivity.Stop();
            grpcSpan.Stop();
        }
    }

    [Fact]
    public void Inject_OtherActivity_IsSentUnchanged()
    {
        var activity = new Activity("some_operation").SetIdFormat(ActivityIdFormat.W3C).Start();
        try
        {
            Assert.Equal(activity.Id, Inject(activity)["traceparent"]);
        }
        finally
        {
            activity.Stop();
        }
    }

    [Fact]
    public void Inject_FallbackActivityWithNoParent_IsSent()
    {
        var previous = Activity.Current;
        Activity.Current = null;
        var httpActivity = new Activity(GrpcTraceContextPropagator.HttpClientActivityName).SetIdFormat(ActivityIdFormat.W3C).Start();
        try
        {
            Assert.Equal(httpActivity.Id, Inject(httpActivity)["traceparent"]);
        }
        finally
        {
            httpActivity.Stop();
            Activity.Current = previous;
        }
    }
}
#endif
