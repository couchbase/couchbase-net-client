using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.Management;

[Collection(CombinationTestingCollection.Name)]
public class BucketManagerTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;
    private TestHelper _testHelper;
    private volatile bool _isBucketFlushed;

    public BucketManagerTests(CouchbaseFixture fixture,
        ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
        _testHelper = new TestHelper(fixture);
    }

    [Fact]
    public async Task
        Test_Create_Couchstore_Bucket_With_History_Throws_InvalidArgument()
    {
        var bucketId = "TestBucket-" + Guid.NewGuid();

        var settings = new BucketSettings
        {
            Name = bucketId,
            BucketType = BucketType.Couchbase,
            RamQuotaMB = 1024,
            FlushEnabled = true,
            NumReplicas = 0,
            ReplicaIndexes = false,
            ConflictResolutionType = null,
            EvictionPolicy = null,
            MaxTtl = 0,
            CompressionMode = null,
            DurabilityMinimumLevel = DurabilityLevel.None,
            StorageBackend = StorageBackend.Couchstore,
            HistoryRetentionCollectionDefault = true,
            HistoryRetentionBytes = null,
            HistoryRetentionDuration = TimeSpan.FromSeconds(5)
        };

        var bucketManager = _fixture.Cluster.Buckets;

        await Assert.ThrowsAsync<InvalidArgumentException>(async () =>
            await bucketManager.CreateBucketAsync(settings,
                    options => options.Timeout(TimeSpan.FromSeconds(10)))
                );

        await bucketManager.DropBucketAsync(bucketId);
    }

    [CouchbaseVersionDependentFact(MinVersion = "7.2.0")]
    public async Task Test_Can_Create_Magma_Bucket_With_History()
    {
        var bucketId = "TestBucket-" + Guid.NewGuid();

        var settings = new BucketSettings
        {
            Name = bucketId,
            BucketType = BucketType.Couchbase,
            RamQuotaMB = 1024,
            FlushEnabled = true,
            NumReplicas = 0,
            ReplicaIndexes = false,
            ConflictResolutionType = null,
            EvictionPolicy = null,
            CompressionMode = null,
            DurabilityMinimumLevel = DurabilityLevel.None,
            StorageBackend = StorageBackend.Magma,
            HistoryRetentionCollectionDefault = true,
            HistoryRetentionBytes = 2147483649,
            HistoryRetentionDuration = null
        };

        var bucketManager = _fixture.Cluster.Buckets;
        try
        {
            await bucketManager.CreateBucketAsync(settings,
                    options => options.Timeout(TimeSpan.FromSeconds(10)))
                ;
        }
        catch (InvalidArgumentException e)
        {
            if (e.Context.Message.Contains(
                    "RAM quota specified is too large to be provisioned into this cluster")) //The op might fail because the cluster is too small
            {
                _outputHelper.WriteLine(
                    "Test failed due to Cluster being too small to add a Magma Bucket.");
                Assert.True(true);
            }
        }
        catch (Exception e)
        {
            _outputHelper.WriteLine(e.Message);
            Assert.True(false);
        }

        await _testHelper.WaitUntilBucketIsPresent(bucketId)
            ;

        var getBucket = await _fixture.Cluster.Buckets.GetBucketAsync(bucketId)
            ;
        Assert.NotNull(getBucket);

        await bucketManager.DropBucketAsync(bucketId);
    }

    [Fact]
    public async Task Test_GetBucketAsync_With_Null_BucketName()
    {
        var bucketManager = _fixture.Cluster.Buckets;

        await Assert.ThrowsAsync<ArgumentNullException>(() => bucketManager.GetBucketAsync(null));
    }
}
