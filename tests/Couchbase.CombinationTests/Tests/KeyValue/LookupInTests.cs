using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.CombinationTests.Tests.KeyValue;

[Collection(CombinationTestingCollection.Name)]
public class LookupInTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;
    private volatile bool _isBucketFlushed = false;

    public LookupInTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task Test_LookupIn_With_RawBinaryTranscoder()
    {
        _isBucketFlushed = await _fixture.FlushBucket(_isBucketFlushed).ConfigureAwait(false);

        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var result = await collection.LookupInAsync(id, specs => specs.Get("name"), new LookupInOptions().Transcoder(new RawBinaryTranscoder())).ConfigureAwait(false);

        var resultAsBytes = result.ContentAs<byte[]>(0);
        var resultAsString = Encoding.UTF8.GetString(resultAsBytes!).Replace("\"", "");

        Assert.Equal(resultAsString, id);

        await collection.RemoveAsync(id).ConfigureAwait(false);
    }

    [Fact]
    public async Task Test_LookupIn_With_JsonTranscoder()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var result = await collection.LookupInAsync(id, specs => specs.Get("name"), new LookupInOptions().Transcoder(new JsonTranscoder())).ConfigureAwait(false);

        var resultAsJson = result.ContentAs<JsonNode>(0);
        var resultAsString = resultAsJson!.ToString();

        Assert.Equal(resultAsString, id);

        await collection.RemoveAsync(id).ConfigureAwait(false);
    }

    [Fact]
    public async Task Test_LookupIn_With_RawStringTranscoder()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var result = await collection.LookupInAsync(id, specs => specs.Get("name"), new LookupInOptions().Transcoder(new RawStringTranscoder())).ConfigureAwait(false);

        var resultAsString = result.ContentAs<string>(0)!.Trim('"');

        Assert.Equal(id, resultAsString);

        await collection.RemoveAsync(id).ConfigureAwait(false);
    }

    [Fact]
    public async Task Test_LookupInAllReplicas_Returns_Results_Marked_With_IsReplica()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var specs = new List<LookupInSpec>();
        specs.Add(LookupInSpec.Get("name"));

        var result = collection.LookupInAllReplicasAsync(id, specs);
        var allResults = await result.ToListAsync();

        //If this test is run on a single-node cluster, or a bucket with no replicas, ignore the validation.
        if (allResults.Count > 1)
        {
            Assert.Contains(allResults, replicaResult => replicaResult.IsReplica == true);
        }

        await collection.RemoveAsync(id).ConfigureAwait(false);
    }

}
