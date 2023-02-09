using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Management.Query;
using Couchbase.Query;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Management.Query;

public class CollectionQueryIndexManagerTests
{
    [Fact]
    public void Default_GetAllIndexes_Contains_QueryContext()
    {
        var queryCollectionIndexManager =
            CreateQueryCollectionIndexManager("default", "_default", "_default", out FakeQueryClient queryClient);

        queryCollectionIndexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
        var actual = queryClient.LastStatement;
        var expected = "SELECT idx.* FROM system:indexes AS idx WHERE ((bucket_id=$bucketName AND scope_id=$scopeName AND keyspace_id=$collectionName) OR (bucket_id IS MISSING and keyspace_id=$bucketName)) AND `using`=\"gsi\" ORDER BY is_primary DESC, name ASC";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NonDefault_GetAllIndexes_Contains_QueryContext()
    {
        var queryCollectionIndexManager =
            CreateQueryCollectionIndexManager("bucket", "scope", "collection", out FakeQueryClient queryClient);

        queryCollectionIndexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
        var actual = queryClient.LastStatement;
        var expected = "SELECT idx.* FROM system:indexes AS idx WHERE (bucket_id=$bucketName AND scope_id=$scopeName AND keyspace_id=$collectionName) AND `using`=\"gsi\" ORDER BY is_primary DESC, name ASC";

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BucketLevel_GetAllIndexes_Contains_QueryContext()
    {
        var queryCollectionIndexManager =
            CreateQueryCollectionIndexManager("bucket", null, null, out FakeQueryClient queryClient);

        queryCollectionIndexManager.GetAllIndexesAsync(new GetAllQueryIndexOptions());
        var actual = queryClient.LastStatement;
        var expected =
            "SELECT idx.* FROM system:indexes AS idx WHERE ((bucket_id IS MISSING AND keyspace_id = $bucketName) OR bucket_id = $bucketName) AND `using`=\"gsi\" ORDER BY is_primary DESC, name ASC";

        Assert.Equal(expected, actual);
    }

    private ICollectionQueryIndexManager CreateQueryCollectionIndexManager(string bucketName, string scopeName, string collectionName, out FakeQueryClient queryClient)
    {
        queryClient = new FakeQueryClient();
        var queryIndexManager = new QueryIndexManager(queryClient, new Mock<ILogger<QueryIndexManager>>().Object,
            new Redactor(new TypedRedactor(RedactionLevel.None)));

        var queryCollectionIndexManager = new CollectionQueryIndexManager(queryIndexManager);
        var options = new ClusterOptions();
        var bucket = new FakeBucket(bucketName, options);
        var scope = new FakeScope(scopeName, bucket, options);
        var collection = new FakeCollection(collectionName, scope, bucket, options);
        ((ICollectionQueryIndexManager)queryCollectionIndexManager).Collection = collection;
        ((ICollectionQueryIndexManager)queryCollectionIndexManager).Bucket = bucket;

        return queryCollectionIndexManager;
    }

    private class FakeQueryClient : IQueryClient
    {
        public int InvalidateQueryCache()
        {
            throw new NotImplementedException();
        }

        public DateTime? LastActivity { get; }
        public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options)
        {
            options.Statement("SELECT 1;");
            LastStatement = statement;
            FormValues = options.GetFormValues();
            return Task.FromResult((IQueryResult<T>) new FakeQueryResult<T>());
        }

        public IDictionary<string, object?> FormValues { get; private set; }

        public string LastStatement { get; private set; }
    }

    private class FakeQueryResult<T> : IQueryResult<T>
    {
        private IEnumerable<T>? _rows = Enumerable.Empty<T>();
        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
        {
            return _rows.ToAsyncEnumerable().GetAsyncEnumerator();
        }new

            public RetryReason RetryReason { get; }
        public IAsyncEnumerable<T> Rows => this;
        public QueryMetaData MetaData { get; }
        public List<Error> Errors { get; }
    }
}
