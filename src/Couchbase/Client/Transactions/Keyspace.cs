
using System.Threading;
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
        public async Task<ICouchbaseCollection> ToCouchbaseCollection(ICluster cluster, CancellationToken cancellationToken = default)
        {
            // BucketAsync/ScopeAsync/CollectionAsync don't take a token, so honor cancellation at the step
            // boundaries: enough for a shutdown to abandon resolution of a still-warming bucket.
            cancellationToken.ThrowIfCancellationRequested();
            var bucket = await cluster.BucketAsync(BucketName).CAF();
            cancellationToken.ThrowIfCancellationRequested();
            var scope = await bucket.ScopeAsync(ScopeName).CAF();
            cancellationToken.ThrowIfCancellationRequested();
            return await scope.CollectionAsync(CollectionName).CAF();
        }
    }
}
