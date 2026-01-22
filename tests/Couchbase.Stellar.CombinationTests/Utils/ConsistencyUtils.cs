using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Stellar.CombinationTests.Fixtures;

namespace Couchbase.Stellar.CombinationTests.Utils;

public class ConsistencyUtils(StellarFixture fixture)
{
    private readonly StellarFixture? _fixture = fixture;

    public async Task WaitUntilBucketIsPresent(string bucketName, int limit = 10)
    {
        bool isPresent = false;
        short retryCount = 0;
        while (!isPresent && retryCount < 10)
        {
            try
            {
                Debug.Assert(_fixture != null, nameof(_fixture) + " != null");
                await _fixture.StellarCluster.Buckets.GetBucketAsync(bucketName);
            }
            catch (Exception)
            {
                retryCount++;
                await Task.Delay(LinearBackoff(retryCount));
                continue;
            }
            isPresent = true;
        }

        if (!isPresent) throw new CouchbaseException($"The consistency util failed out after {limit} attempts.");
    }

    public async Task WaitUntilScopeIsPresent(string scopeName = "_default", string bucketName = "default", int limit = 10)
    {
        bool isPresent = false;
        short retryCount = 0;
        while (!isPresent && retryCount < 10)
        {
            try
            {
                Debug.Assert(_fixture != null, nameof(_fixture) + " != null");
                var bucket = await _fixture.StellarCluster.BucketAsync(bucketName);
                await bucket.ScopeAsync(scopeName);
            }
            catch (Exception)
            {
                retryCount++;
                await Task.Delay(LinearBackoff(retryCount));
                continue;
            }
            isPresent = true;
        }

        if (!isPresent) throw new CouchbaseException($"The consistency util failed out after {limit} attempts.");
    }

    public async Task WaitUntilCollectionIsPresent(string collectionName, string? bucketName = null, string? scopeName = null, int limit = 10)
    {
        bool isPresent = false;
        short retryCount = 0;
        var docId = Guid.NewGuid().ToString();
        while (!isPresent && retryCount < limit)
        {
            try
            {
                Debug.Assert(_fixture != null, nameof(_fixture) + " != null");
                var bucket = await _fixture.StellarCluster.BucketAsync(bucketName ?? "default");
                var scope = await bucket.ScopeAsync(scopeName ?? "_default");
                var collection = await scope.CollectionAsync(collectionName ?? "_default");
                await collection.UpsertAsync(docId, new { Content = "Content" });
            }
            catch (Exception)
            {
                retryCount++;
                await Task.Delay(LinearBackoff(retryCount));
                continue;
            }
            isPresent = true;
        }

        if (!isPresent) throw new CouchbaseException($"The consistency util failed out after {limit} attempts.");
    }

    public async Task WaitUntilCollectionIsDropped(string collectionName, string? bucketName = null, string? scopeName = null, int limit = 10)
    {
        Debug.Assert(_fixture != null, nameof(_fixture) + " != null");
        var bucket = await _fixture.StellarCluster.BucketAsync(bucketName ?? "default");
        var scope = await bucket.ScopeAsync(scopeName ?? "_default");

        bool isGone = false;
        short retryCount = 0;
        while (!isGone && retryCount < limit)
        {
            try
            {
                var collection = await scope.CollectionAsync(collectionName);
                await collection.UpsertAsync(Guid.NewGuid().ToString(), new { TestContent = "Test" });
            }
            catch (Exception)
            {
                isGone = true;
                break;
            }
            retryCount++;
            await Task.Delay(LinearBackoff(retryCount));
        }

        if (!isGone) throw new CouchbaseException($"The consistency util failed out after {limit} attempts.");
    }

    public async Task WaitUntilDocumentIsPresent(string id, string? collectionName = null, string? bucketName = null, string? scopeName = null, int limit = 10)
    {
        IGetResult? result = null;

        var isPresent = false;
        short retryCount = 0;
        while (!isPresent && retryCount < limit)
        {
            try
            {
                Debug.Assert(_fixture != null, nameof(_fixture) + " != null");
                var bucket = await _fixture.StellarCluster.BucketAsync(bucketName ?? "default");
                var scope = await bucket.ScopeAsync(scopeName ?? "_default");
                var collection = await scope.CollectionAsync(collectionName ?? "_default");
                result = await collection.GetAsync(id);
            }
            catch (Exception)
            {
                retryCount++;
                await Task.Delay(LinearBackoff(retryCount));
            }

            if (result != null)
            {
                break;
            }

            if (!isPresent) throw new CouchbaseException($"The consistency util failed out after {limit} attempts.");
        }
    }

    private static TimeSpan LinearBackoff(int retryCount)
    {
        return TimeSpan.FromMilliseconds(retryCount * 200);
    }
}
