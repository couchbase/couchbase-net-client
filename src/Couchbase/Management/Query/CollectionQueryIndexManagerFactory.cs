using System;
using Couchbase.KeyValue;
using Couchbase.Management.Query;

namespace Couchbase.Management.Collections;

internal class CollectionQueryIndexManagerFactory : ICollectionQueryIndexManagerFactory
{
    private readonly IQueryIndexManager _queryIndexManager;

    public CollectionQueryIndexManagerFactory(IQueryIndexManager queryIndexManager)
    {
        _queryIndexManager = queryIndexManager ?? throw new ArgumentNullException(nameof(queryIndexManager));
    }

    public ICollectionQueryIndexManager Create(IBucket bucket, ICouchbaseCollection collection)
    {
        return new CollectionQueryIndexManager(_queryIndexManager, bucket, collection);
    }
}
