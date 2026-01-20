using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
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
        _isBucketFlushed = await _fixture.FlushBucket(_isBucketFlushed);

        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection();

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var result = await collection.LookupInAsync(id, specs => specs.Get("name"), new LookupInOptions().Transcoder(new RawBinaryTranscoder()));

        var resultAsBytes = result.ContentAs<byte[]>(0);
        var resultAsString = Encoding.UTF8.GetString(resultAsBytes!).Replace("\"", "");

        Assert.Equal(resultAsString, id);

        await collection.RemoveAsync(id);
    }

    [Fact]
    public async Task Test_LookupIn_With_JsonTranscoder()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection();

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var result = await collection.LookupInAsync(id, specs => specs.Get("name"), new LookupInOptions().Transcoder(new JsonTranscoder()));

        var resultAsJson = result.ContentAs<JsonNode>(0);
        var resultAsString = resultAsJson!.ToString();

        Assert.Equal(resultAsString, id);

        await collection.RemoveAsync(id);
    }

    [Fact]
    public async Task Test_LookupIn_With_RawStringTranscoder()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection();

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var result = await collection.LookupInAsync(id, specs => specs.Get("name"), new LookupInOptions().Transcoder(new RawStringTranscoder()));

        var resultAsString = result.ContentAs<string>(0)!.Trim('"');

        Assert.Equal(id, resultAsString);

        await collection.RemoveAsync(id);
    }

    [Fact]
    public async Task Test_LookupInAllReplicas_Returns_Results_Marked_With_IsReplica()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection();

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

        await collection.RemoveAsync(id);
    }

    [Fact]
    public async Task Test_LookupInAnyReplica_DocumentUnretrievable_Gets_Thrown()
    {
        var collection = await _fixture.GetDefaultCollection();

        var specs = new List<LookupInSpec>();
        specs.Add(LookupInSpec.Get("name"));

        var error = await Record.ExceptionAsync(() => collection.LookupInAnyReplicaAsync("wrongId", specs));
        Assert.IsType<DocumentUnretrievableException>(error);
        Assert.True(((DocumentUnretrievableException)error).InnerExceptions.Count >= 1);
    }

    [Fact]
    public async Task Test_LookupInAnyReplica_Timeout_Gets_Thrown()
    {
        var collection = await _fixture.GetDefaultCollection();

        var specs = new List<LookupInSpec>();
        specs.Add(LookupInSpec.Get("name"));

        var error = await Record.ExceptionAsync(() => collection.LookupInAnyReplicaAsync("wrongId", specs, options => options.Timeout(TimeSpan.FromMilliseconds(1))));
        Assert.IsType<UnambiguousTimeoutException>(error);
    }

    [Fact]
    public async Task Test_All_LookupIn_Should_Throw_InvalidArgument_If_Too_Many_Specs()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection();

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var specs = new List<LookupInSpec>();
        foreach (var _ in Enumerable.Range(0, 17))
        {
            specs.Add(LookupInSpec.Get("name"));
        }

        await Assert.ThrowsAsync<InvalidArgumentException>(() => collection.LookupInAnyReplicaAsync(id, specs));
        await Assert.ThrowsAsync<InvalidArgumentException>(() => collection.LookupInAsync(id, specs));

        var allReplicas = collection.LookupInAllReplicasAsync(id, specs);
        await Assert.ThrowsAsync<InvalidArgumentException>(async () =>
        {
            await foreach (var _ in allReplicas)
            {
                continue;
            }
        });

        await collection.RemoveAsync(id);
    }

    [Fact]
    public async Task Test_LookupInAnyReplica_Extensions()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection();

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var specs = new List<LookupInSpec>();
        specs.Add(LookupInSpec.Get("name"));

        var anyReplicaSpecBuilder = await collection.LookupInAnyReplicaAsync(id,
            builder => builder.Get("name"));

        var anyReplicaOptionsBuilder = await collection.LookupInAnyReplicaAsync(id, specs,
            options => options.Timeout(TimeSpan.FromSeconds(10)));

        var anyReplicaSpecAndOptionsBuilder = await collection.LookupInAnyReplicaAsync(id,
            builder => builder.Get("name"),
            options => options.Timeout(TimeSpan.FromSeconds(10)));

        Assert.Equal(id, anyReplicaSpecBuilder.ContentAs<string>(0));
        Assert.Equal(id, anyReplicaOptionsBuilder.ContentAs<string>(0));
        Assert.Equal(id, anyReplicaSpecAndOptionsBuilder.ContentAs<string>(0));

        await collection.RemoveAsync(id);
    }

    [Fact]
    public async Task Test_LookupInAllReplicas_Extensions()
    {
        var id = "Test-" + Guid.NewGuid();
        var collection = await _fixture.GetDefaultCollection();

        await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

        var specs = new List<LookupInSpec>();
        specs.Add(LookupInSpec.Get("name"));

        var allReplicasSpecBuilder = collection.LookupInAllReplicasAsync(id,
            builder => builder.Get("name"));

        var allReplicasOptionsBuilder = collection.LookupInAllReplicasAsync(id, specs,
            options => options.Timeout(TimeSpan.FromSeconds(10)));

        var allReplicasSpecAndOptionsBuilder = collection.LookupInAllReplicasAsync(id,
            builder => builder.Get("name"),
            options => options.Timeout(TimeSpan.FromSeconds(10)));

        await foreach (var item in allReplicasSpecBuilder)
        {
            Assert.Equal(id, item.ContentAs<string>(0));
        }
        await foreach (var item in allReplicasOptionsBuilder)
        {
            Assert.Equal(id, item.ContentAs<string>(0));
        }
        await foreach (var item in allReplicasSpecAndOptionsBuilder)
        {
            Assert.Equal(id, item.ContentAs<string>(0));
        }

        await collection.RemoveAsync(id);
    }

}
