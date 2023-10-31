using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient;

public class StellarQuery
{
    private readonly ITestOutputHelper _outputHelper;

    public StellarQuery(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task ClusterQuery()
    {
        var clusterOptions = new ClusterOptions()
        {
            UserName = "Administrator",
            Password = "password",
            HttpIgnoreRemoteCertificateMismatch = true,
        };

        var cluster = await StellarUtils.GetCluster("protostellar");
        var queryResult = await cluster.QueryAsync<dynamic>("SELECT * FROM `_default`");
        await foreach (var result in queryResult)
        {
            _outputHelper.WriteLine(result.ToString());
        }
    }

    [Fact]
    public async Task ScopedQuery()
    {
        var clusterOptions = new ClusterOptions()
        {
            UserName = "Administrator",
            Password = "password",
            HttpIgnoreRemoteCertificateMismatch = true,
        };

        var cluster = await StellarClient.ConnectAsync("protostellar://localhost", clusterOptions);
        var bucket1 = await cluster.BucketAsync("default");
        var scope = bucket1.DefaultScope();
        var queryResult = await scope.QueryAsync<object>("SELECT * FROM `_default`");
        await foreach (var result in queryResult)
        {
            _outputHelper.WriteLine(result.ToString());
        }
    }
}
