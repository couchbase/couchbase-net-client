using System.Threading.Tasks;
using Couchbase.Analytics;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient;

public class StellarAnalytics
{
    private readonly ITestOutputHelper _outputHelper;

    public StellarAnalytics(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task ClusterAnalytics()
    {
        var clusterOptions = new ClusterOptions
        {
            UserName = "Administrator",
            Password = "password",
            HttpIgnoreRemoteCertificateMismatch = true,
            KvIgnoreRemoteCertificateNameMismatch = true
        };

        var cluster = await StellarCluster.ConnectAsync("protostellar://localhost", clusterOptions);
        var bucket = await cluster.BucketAsync("default");
        var scope = await bucket.ScopeAsync("_default");


        var analyticsOptions = new AnalyticsOptions().Priority(true).ClientContextId("").Readonly(true);

        var analyticsResult = await scope.AnalyticsQueryAsync<dynamic>("SELECT \"hello\" as greeting;", analyticsOptions).ConfigureAwait(false);

        await foreach (var result in analyticsResult)
        {
            _outputHelper.WriteLine(result.ToString());
        }
    }
}
