using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Fixtures;
using Couchbase.Test.Common.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests
{
    public class ReplaceTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public ReplaceTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [CouchbaseVersionDependentTheory(MinVersion = "7.0.0")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Replace_With_PreserveTtl(bool preserveTtl)
        {
            var docId = "ReplaceTests.Replace_With_PreserveTtl";
            var collection = await _fixture.GetDefaultCollectionAsync();

            await using var docDisposer = DisposeCleaner.RemoveDocument(collection, docId, _outputHelper);
            await collection.InsertAsync(docId, new {Bar = "Foo"},
                options => { options.Expiry(TimeSpan.FromMinutes(1)); });
            var result =
                await collection.ReplaceAsync(docId, new {Foo = "Bar"}, new ReplaceOptions().PreserveTtl(preserveTtl));
            Assert.True(result.Cas > 0);
        }
    }
}
