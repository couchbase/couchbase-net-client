using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.KeyValue;

namespace Couchbase.Integrated.Transactions;

[InterfaceStability(Level.Volatile)]
public record KeySpace(string Bucket, string Scope, string Collection)
{
    public async Task<ICouchbaseCollection> GetCollectionAsync(ICluster cluster)
    {
        var bkt = await cluster.BucketAsync(Bucket).CAF();
        var scp = bkt.Scope(Scope);
        var col = scp.Collection(Collection);
        return col;
    }
}







