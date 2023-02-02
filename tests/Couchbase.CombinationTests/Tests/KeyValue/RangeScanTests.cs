using System;
using System.Linq;
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

    [Fact]
    public async Task Foo()
    {
        var random = new Random();

        var coll = await _fixture.GetDefaultCollection();

        for (var xd = 3; xd < 10000; xd++)
        {
            var doc = new String('*', random.Next(100, 2048));
            doc = doc.Insert(0, "start");
            doc = doc.Insert(doc.Length, "end");
            await coll.UpsertAsync($"key{xd}", doc, new UpsertOptions().Timeout(TimeSpan.FromSeconds(20))).ConfigureAwait(false);
        }

        var scan = coll.ScanAsync(
            new RangeScan(ScanTerm.Inclusive("key"), ScanTerm.Inclusive("key9999")),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(20000)).IdsOnly(false));

        var count = 0;
        await foreach (var scanResult in scan)
        {
            count++;
            //_outputHelper.WriteLine(scanResult.Id);
            var content = scanResult.ContentAsString();
            if (!(content.StartsWith("\"start") && content.EndsWith("end\"")))
            {
                    _outputHelper.WriteLine($"Key {scanResult.Id} content is bad.");
            }
        }

        _outputHelper.WriteLine(count.ToString());
    }

    [Fact]
    public async Task Test_MaxDocumentSize()
    {
        var coll = await _fixture.GetDefaultCollection();
        var id = "Test_MaxDocumentSize";
        var doc = new string('*', 20000000);
        doc = doc.Insert(0, "start");
        doc = doc.Insert(doc.Length, "end");

        await coll.UpsertAsync(id, doc).ConfigureAwait(false);

        var scan = coll.ScanAsync(
            new RangeScan(ScanTerm.Inclusive("Test_"), ScanTerm.Inclusive("Test_MaxDocumentSize")),
            new ScanOptions().Timeout(TimeSpan.FromSeconds(20000)).IdsOnly(false).ItemLimit(1));

        var scanResult = await scan.FirstAsync().ConfigureAwait(false);
        var content = scanResult.ContentAs<string>();
        Assert.True(content.StartsWith("\"start"));
        Assert.True(content.EndsWith("end\""));
        Assert.Equal(doc.Length, content.Length);
    }
}
