using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests
{
    public class UpsertTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public UpsertTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [CouchbaseVersionDependentTheory(MinVersion = "7.0.0")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Upsert_With_PreserveTtl(bool preserveTtl)
        {
            var docId = "UpsertTests.Upsert_With_PreserveTtl";
            var collection = await _fixture.GetDefaultCollectionAsync();

            await using var docDisposer = DisposeCleaner.RemoveDocument(collection, docId, _outputHelper);
            await collection.InsertAsync(docId, new {Bar = "Foo"},
                options => { options.Expiry(TimeSpan.FromMinutes(1)); });

            var result =
                await collection.UpsertAsync(docId, new {Foo = "Bar"}, new UpsertOptions().PreserveTtl(preserveTtl));
            Assert.True(result.Cas > 0);
        }
    }
}
