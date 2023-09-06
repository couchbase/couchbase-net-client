using Couchbase.KeyValue;
using Couchbase.Management.Query;

namespace Couchbase.Management.Query;

public interface ICollectionQueryIndexManagerFactory
{
    ICollectionQueryIndexManager Create(IBucket bucket, ICouchbaseCollection collection);
}
