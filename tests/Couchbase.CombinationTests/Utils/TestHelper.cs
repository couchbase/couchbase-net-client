using System;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.CombinationTests.Fixtures;

public class TestHelper
{
    private CouchbaseFixture? _fixture;

    public TestHelper(CouchbaseFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task WaitUntilBucketIsPresent(string bucketName)
    {
        bool isPresent = false;
        short retryCount = 0;
        while (!isPresent)
        {
            try
            {
                await _fixture.Cluster.Buckets.GetBucketAsync(bucketName).ConfigureAwait(false);
            }
            catch (Exception)
            {
                retryCount++;
                await Task.Delay(LinearBackoff(retryCount)).ConfigureAwait(false);
                continue;
            }
            isPresent = true;
        }
    }

    public async Task WaitUntilCollectionIsPresent(string collectionName, string? bucketName = null, string? scopeName = null)
    {
        bool isPresent = false;
        short retryCount = 0;
        while (!isPresent)
        {
            try
            {
                var bucket = await _fixture.Cluster.BucketAsync(bucketName ?? "default").ConfigureAwait(false);
                var scope = await bucket.ScopeAsync(scopeName ?? "_default").ConfigureAwait(false);
                await scope.CollectionAsync(collectionName).ConfigureAwait(false);
            }
            catch (Exception)
            {
                retryCount++;
                await Task.Delay(LinearBackoff(retryCount)).ConfigureAwait(false);
                continue;
            }
            isPresent = true;
        }
    }

    private static TimeSpan LinearBackoff(int retryCount)
    {
        return TimeSpan.FromMilliseconds(retryCount * 100);
    }
}
