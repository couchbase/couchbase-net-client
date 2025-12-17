using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Stellar.CombinationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.Analytics;

[Collection(StellarTestCollection.Name)]
public class StellarAnalyticsTests
{
    private readonly ITestOutputHelper _outputHelper;
    private StellarFixture _fixture;

    public StellarAnalyticsTests(StellarFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task ClusterAnalytics()
    {
        var analyticsOptions = new AnalyticsOptions().Priority(true).ClientContextId("").Readonly(true);

        var analyticsResult = await _fixture.StellarCluster.AnalyticsQueryAsync<dynamic>("SELECT \"hello\" as greeting;", analyticsOptions).ConfigureAwait(true);

        await foreach (var result in analyticsResult)
        {
            _outputHelper.WriteLine(result.ToString());
        }
    }
}
