
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.KeyValue;
using Xunit;
using Xunit.Abstractions;
namespace Couchbase.CombinationTests.Tests.KeyValue;

/// <summary>
/// Tests for <see cref="ICouchbaseCollection"/> extension methods that supplement the regular KV CRUD methods.
/// </summary>
[Collection(CombinationTestingCollection.Name)]
public class CollectionExtensionTests
{
    private readonly CouchbaseFixture _fixture;
    private readonly ITestOutputHelper _outputHelper;
    public CollectionExtensionTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
    {
        _fixture = fixture;
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task Test_TryGetAsync_KeyNotFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();
        var getResult = await col.TryGetAsync(doc1);
        Assert.False(getResult.Exists);
    }

    [Fact]
    public async Task Test_TryGetAsync_KeyFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();

        try
        {
            await col.UpsertAsync(doc1, new { DocThatExists = true });
            var getResult = await col.TryGetAsync(doc1);
            Assert.True(getResult.Exists);
            var content = getResult.ContentAs<dynamic>();
            Assert.NotNull(content);
        }
        finally
        {
            await col.TryRemoveAsync(doc1);
        }
    }

    [Fact]
    public async Task Test_TryRemoveAsync_KeyNotFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();
        var getResult = await col.TryRemoveAsync(doc1);
        Assert.False(getResult.Exists);
    }

    [Fact]
    public async Task Test_TryRemoveAsync_KeyFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();

        try
        {
            await col.UpsertAsync(doc1, new { DocThatExists = true });
            var removeResult = await col.TryRemoveAsync(doc1);

            Assert.True(removeResult.Exists);
        }
        finally
        {
            await col.TryRemoveAsync(doc1);
        }
    }

    [Fact]
    public async Task Test_TryUnlockAsync_KeyNotFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();

        var unlockResult = await col.TryUnlockAsync(doc1, 1);

        Assert.False(unlockResult.Exists);
    }

    [Fact]
    public async Task Test_TryUnlockAsync_KeyFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();

        try
        {
            await col.UpsertAsync(doc1, new { DocThatExists = true }).ConfigureAwait(false);
            var getResult = await col.GetAndLockAsync(doc1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            var unlockResult = await col.TryUnlockAsync(doc1, getResult.Cas).ConfigureAwait(false);
            Assert.True(unlockResult.Exists);
        }
        finally
        {
            await col.TryRemoveAsync(doc1);
        }
    }

    [Fact]
    public async Task Test_GetAndLockAsync_KeyNotFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();

        var unlockResult = await col.TryGetAndLockAsync(doc1, TimeSpan.FromSeconds(1));
        Assert.False(unlockResult.Exists);
    }

    [Fact]
    public async Task Test_GetAndLockAsync_KeyFound()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();

        try
        {
            await col.UpsertAsync(doc1, new {DocThatExists = true}).ConfigureAwait(false);
            var getResult = await col.TryGetAndLockAsync(doc1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            Assert.True(getResult.Exists);
            await col.UnlockAsync(doc1, getResult.Cas);
        }
        finally
        {
            await col.TryRemoveAsync(doc1);
        }
    }

    [Fact]
    public async Task Test_TryTouchAsync()
    {
        var col = await _fixture.GetDefaultCollection();
        var doc1 = Guid.NewGuid().ToString();

        await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(2)));
        var upsertResult = await col.ExistsAsync(doc1);
        Assert.True(upsertResult.Exists);

        var tryTouchResult = await col.TryTouchAsync(doc1, TimeSpan.FromSeconds(2));
        Assert.True(tryTouchResult.Exists);
        Assert.NotEqual(0ul, tryTouchResult?.MutationResult?.Cas);
        await Task.Delay(TimeSpan.FromSeconds(3));
        var tryTouchExistsResult = await col.ExistsAsync(doc1);
        Assert.False(tryTouchExistsResult.Exists);

        var tryTouchNegativeResult = await col.TryTouchAsync(doc1 + "fake", TimeSpan.FromSeconds(2));
        Assert.False(tryTouchNegativeResult.Exists);
    }
}
