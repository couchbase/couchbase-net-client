using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Microsoft.VisualBasic;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.KeyValue;

[Collection(CombinationTestingCollection.Name)]
public class RangeScanTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;

    public RangeScanTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    public async void FlushBucket()
    {
        var bucket = _fixture.GetDefaultCollection().Result.Scope.Bucket;
        await bucket.Cluster.Buckets.FlushBucketAsync(bucket.Name);
    }

    [Fact]
    public async Task Test_RangeScan()
    {
        var random = new Random();

        var coll = await _fixture.GetDefaultCollection();

        for (var xd = 0; xd < 10_000; xd++)
        {
            var doc = new String('*', random.Next(100, 2048));
            doc = doc.Insert(0, "start");
            doc = doc.Insert(doc.Length, "end");
            await coll.UpsertAsync($"key{xd}", doc, new UpsertOptions().Timeout(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
        }

        var scan = coll.ScanAsync(
            new RangeScan(ScanTerm.Inclusive("key"), ScanTerm.Inclusive("key9999")),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(20_000)).IdsOnly(false));

        var count = 0;
        await foreach (var scanResult in scan)
        {
            count++;
            //_outputHelper.WriteLine(scanResult.Id);
            var content = scanResult.ContentAsString();
            if (!(content.StartsWith("\"start") && content.EndsWith("end\"")))
            {
                _outputHelper.WriteLine($"Key {scanResult.Id} content is bad. Content start is: {content.Substring(0, 6)}");
            }
        }

        Assert.Equal(10_000, count);

        _outputHelper.WriteLine(count.ToString());

        for (var xd = 0; xd < 10_000; xd++)
        {
            await coll.RemoveAsync($"key{xd}").ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Test_MaxDocumentSize()
    {
        var coll = await _fixture.GetDefaultCollection();
        var id = "Test_MaxDocumentSize";
        var doc = new string('*', 20_000_000);
        doc = doc.Insert(0, "start");
        doc = doc.Insert(doc.Length, "end");

        await coll.UpsertAsync(id, doc).ConfigureAwait(false);

        var scan = coll.ScanAsync(
            new RangeScan(ScanTerm.Inclusive("Test_"), ScanTerm.Inclusive("Test_MaxDocumentSize")),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(20_000)).IdsOnly(false).ItemLimit(1));

        var scanResult = await scan.FirstAsync().ConfigureAwait(false);
        var content = scanResult.ContentAs<string>();
        Assert.StartsWith("start", content);
        Assert.EndsWith("end", content);
        Assert.Equal(doc.Length, content.Length);

        await coll.RemoveAsync(id).ConfigureAwait(false);
    }

    [Fact]
    public async Task Test_SamplingScan()
    {
        var random = new Random();

        var coll = await _fixture.GetDefaultCollection();

        for (var xd = 0; xd < 200; xd++)
        {
            var doc = new String('*', random.Next(100, 2048));
            doc = doc.Insert(0, "start");
            doc = doc.Insert(doc.Length, "end");
            await coll.UpsertAsync($"key{xd}", doc, new UpsertOptions().Timeout(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
        }

        var scan =  coll.ScanAsync(new SamplingScan(100),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(20_000)));

        var count = 0;
        await foreach (var scanResult in scan)
        {
            var content = scanResult.ContentAs<string>();
            Assert.StartsWith("start", content);
            Assert.EndsWith("end", content);
            count++;
        }

        Assert.Equal(100, count);

        for (var xd = 0; xd < 200; xd++)
        {
            await coll.RemoveAsync($"key{xd}").ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Test_Single_Document_SamplingScan()
    {
        var collection = await _fixture.GetDefaultCollection();
        var body = "hello";
        var id = System.Guid.NewGuid().ToString();

        await collection.UpsertAsync(id, body).ConfigureAwait(false);

        var scan = collection.ScanAsync(
            new RangeScan(ScanTerm.Inclusive(id), ScanTerm.Inclusive(id)),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(10)).IdsOnly(false).ItemLimit(1));

        var scanResult = await scan.FirstAsync().ConfigureAwait(false);
        var content = scanResult.ContentAs<string>();

        Assert.Equal(id, scanResult.Id);
        Assert.Equal(body, content);

        await collection.RemoveAsync(id).ConfigureAwait(false);
    }

    [Fact]
    public async Task Test_Use_Minimum_And_Maximum_ScanTerms()
    {
        var collection = await _fixture.GetDefaultCollection();
        var body = "hello";
        var id = System.Guid.NewGuid().ToString();

        for (var xd = 0; xd < 100; xd++)
        {
            await collection.UpsertAsync(id + xd, body).ConfigureAwait(false);
        }

        var min = ScanTerm.Inclusive(id + ScanTerm.Minimum().Id);
        var max = ScanTerm.Inclusive(id + ScanTerm.Maximum().Id);

        Assert.Equal(id + "\0", min.Id);
        Assert.Equal(id + "\U0010FFFF", max.Id);

        Thread.Sleep(1500);

        var scan = collection.ScanAsync(new RangeScan(min, max),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(10)).IdsOnly(false));

        var count = 0;
        await foreach (var scanResult in scan)
        {
            count++;
        }

        Assert.Equal(100, count);

        for (var xd = 0; xd < 100; xd++)
        {
            await collection.RemoveAsync(id + xd).ConfigureAwait(false);
        }

    }
}
