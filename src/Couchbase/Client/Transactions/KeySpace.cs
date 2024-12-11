using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.KeyValue;

namespace Couchbase.Client.Transactions;

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

    public static KeySpace FromCollection(ICouchbaseCollection collection) => new (
        Bucket: collection.Scope.Bucket.Name,
        Scope: collection.Scope.Name,
        Collection: collection.Name);

    // TODO:  escape names?
    internal string ForQueryParameter() => $"default:`{Bucket}`.`{Scope}`.`{Collection}`";
}







