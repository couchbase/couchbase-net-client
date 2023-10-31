using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Utils;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests;

public class StellarUtils
{
    public static ITestOutputHelper OutputHelper;

    public StellarUtils(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }
    public static async Task<ICluster> GetCluster(string protocol)
    {
        var opts = new ClusterOptions()
        {
            UserName = "Administrator",
            Password = "password"
        };

        var connectionString = $"{protocol}://localhost";
        if (connectionString.Contains("//localhost"))
        {
            opts.KvIgnoreRemoteCertificateNameMismatch = true;
            opts.HttpIgnoreRemoteCertificateMismatch = true;
        }

        return await StellarClient.ConnectAsync(connectionString, opts);
    }

    public static async Task<IBucket> GetDefaultBucket(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        return await cluster.BucketAsync("default").ConfigureAwait(false);
    }

    public static async Task<ICouchbaseCollection> GetDefaultCollection(string protocol)
    {
        var cluster = await GetCluster(protocol).ConfigureAwait(false);
        var bucket = await cluster.BucketAsync("default").ConfigureAwait(false);
        var scope = await bucket.ScopeAsync("_default").ConfigureAwait(false);
        var collection = await scope.CollectionAsync("_default").ConfigureAwait(false);
        return collection;
    }
}
