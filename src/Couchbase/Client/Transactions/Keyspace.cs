
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.Client.Transactions
{
    public record Keyspace(string BucketName, string ScopeName, string CollectionName)
    {
        public Keyspace(ICouchbaseCollection collection) :
            this(collection.Scope.Bucket.Name, collection.Scope.Name, collection.Name)
        {}

        public override string ToString() => $"{BucketName}.{ScopeName}.{CollectionName}";
        public async Task<ICouchbaseCollection> ToCouchbaseCollection(ICluster cluster)
        {
            var bucket = await cluster.BucketAsync(BucketName).CAF();
            var scope = await bucket.ScopeAsync(ScopeName).CAF();
            return await scope.CollectionAsync(CollectionName).CAF();
        }
    }
}
