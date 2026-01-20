using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.KeyValue.RangeScan;
using Couchbase.Query;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.KeyValue;

[Collection(CombinationTestingCollection.Name)]
public class RangeScanTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;
    private volatile bool _isBucketFlushed = false;

    public RangeScanTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [CouchbaseVersionDependentFact(MinVersion = "8.0.0")]
    public async Task Test_RangeScan()
    {
        _isBucketFlushed = await _fixture.FlushBucket(_isBucketFlushed);

        var random = new Random();

        var coll = await _fixture.GetDefaultCollection();

        IMutationResult[] mutationResults = new IMutationResult[10_000];
        for (var xd = 0; xd < 10_000; xd++)
        {
            var doc = new String('*', random.Next(100, 2048));
            doc = doc.Insert(0, "start");
            doc = doc.Insert(doc.Length, "end");
            var result = await coll.UpsertAsync($"key{xd}", doc, new UpsertOptions().Timeout(TimeSpan.FromSeconds(20)));
            mutationResults[xd] = result;
        }

        var mutationState = new MutationState();
        mutationState.Add(mutationResults);

        var scan = coll.ScanAsync(
            new RangeScan(ScanTerm.Inclusive("key"), ScanTerm.Inclusive("key9999")),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(20_000)).IdsOnly(false).ConsistentWith(mutationState));

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
            await coll.RemoveAsync($"key{xd}");
        }
    }

    [CouchbaseVersionDependentFact(MinVersion = "8.0.0")]
    public async Task Test_MaxDocumentSize()
    {
        var coll = await _fixture.GetDefaultCollection();
        var id = "Test_MaxDocumentSize";
        var doc = new string('*', 20_000_000);
        doc = doc.Insert(0, "start");
        doc = doc.Insert(doc.Length, "end");

        await coll.UpsertAsync(id, doc);

        var scan = coll.ScanAsync(
            new RangeScan(ScanTerm.Inclusive("Test_"), ScanTerm.Inclusive("Test_MaxDocumentSize")),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(20_000)).IdsOnly(false).ItemLimit(1));

        var scanResult = await scan.FirstAsync();
        var content = scanResult.ContentAs<string>();
        Assert.StartsWith("start", content);
        Assert.EndsWith("end", content);
        Assert.Equal(doc.Length, content.Length);

        await coll.RemoveAsync(id);
    }

    [CouchbaseVersionDependentFact(MinVersion = "8.0.0")]
    public async Task Test_SamplingScan()
    {
        var random = new Random();

        var coll = await _fixture.GetDefaultCollection();

        for (var xd = 0; xd < 200; xd++)
        {
            var doc = new String('*', random.Next(100, 2048));
            doc = doc.Insert(0, "start");
            doc = doc.Insert(doc.Length, "end");
            await coll.UpsertAsync($"key{xd}", doc, new UpsertOptions().Timeout(TimeSpan.FromSeconds(20)));
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
            await coll.RemoveAsync($"key{xd}");
        }
    }

    [CouchbaseVersionDependentFact(MinVersion = "8.0.0")]
    public async Task Test_Single_Document_SamplingScan()
    {
        var collection = await _fixture.GetDefaultCollection();
        var body = "hello";
        var id = System.Guid.NewGuid().ToString();

        await collection.UpsertAsync(id, body);

        var scan = collection.ScanAsync(
            new RangeScan(ScanTerm.Inclusive(id), ScanTerm.Inclusive(id)),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(10)).IdsOnly(false).ItemLimit(1));

        var scanResult = await scan.FirstAsync();
        var content = scanResult.ContentAs<string>();

        Assert.Equal(id, scanResult.Id);
        Assert.Equal(body, content);

        await collection.RemoveAsync(id);
    }

    [CouchbaseVersionDependentFact(MinVersion = "8.0.0")]
    public async Task Test_Use_Minimum_And_Maximum_ScanTerms()
    {
        var collection = await _fixture.GetDefaultCollection();
        var body = "hello";
        var id = System.Guid.NewGuid().ToString();

        for (var xd = 0; xd < 100; xd++)
        {
            await collection.UpsertAsync(id + xd, body);
        }

        var min = ScanTerm.Inclusive(id + Encoding.UTF8.GetString(new byte[] { 0x00 }));
        var max = ScanTerm.Inclusive(id + Encoding.UTF8.GetString(new byte[] { 0xF4, 0x8F, 0xBF, 0xBF }));

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
            await collection.RemoveAsync(id + xd);
        }

    }
}
