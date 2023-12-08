using Couchbase.Core.Exceptions;
using Xunit;

namespace Couchbase.NetClient.CombinationTests;

/// <summary>
/// This tests the public shim which is the gateway into the on-premise and cloud APIs
/// </summary>
public class ClusterTests
{
    [Theory]
    [InlineData("couchbase://localhost", typeof(Couchbase.NetClient.Cluster))]
    [InlineData("couchbases://localhost", typeof(Couchbase.NetClient.Cluster))]
    [InlineData("couchbase2://localhost", typeof(Couchbase.Stellar.StellarCluster))]
    public async Task Test_Schema_Delivers_The_Correct_ICluster_Impl(string connectionString, Type type)
    {
        var cluster = await Couchbase.Cluster.ConnectAsync(connectionString,
            new ClusterOptions().WithCredentials("Administrator", "password"));
        Assert.IsType(type, cluster);
    }

    [Fact]
    public async Task Test_Stellar_Wrong_Connection_String_Throws_ConnectionException()
    {
        var connectionString = "couchbase2://wrongHostname";
        var exception = await Record.ExceptionAsync(
            () => Couchbase.Cluster.ConnectAsync(connectionString,
                new ClusterOptions()
                    .WithCredentials("Administrator", "password")))
            .ConfigureAwait(false);
        Assert.IsType<ConnectException>(exception);
    }
}
