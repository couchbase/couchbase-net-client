using Couchbase.KeyValue;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Couchbase.CombinationTests.Tests.Query
{
    [Collection(CombinationTestingCollection.Name)]
    public class QueryTests
    {
        private readonly CouchbaseFixture _fixture;

        public QueryTests(CouchbaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_Query_Basic()
        {
            await _fixture.BuildAsync();
            var result = await _fixture.Cluster.QueryAsync<dynamic>("SELECT 1;");
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task Test_Query_Update_PreserveExpiry()
        {
            await _fixture.BuildAsync();
            var docId = System.Guid.NewGuid().ToString();
            var doc = new { id = docId, testName = nameof(Test_Query_Update_PreserveExpiry), content = "initial" };
            var collection = await _fixture.GetDefaultCollection();
            var opts = new InsertOptions().Expiry(TimeSpan.FromSeconds(30));
            await collection.InsertAsync(docId, doc, options: opts);

            try
            {
                var result = await _fixture.Cluster.QueryAsync<dynamic>("UPDATE default AS d SET d.content = 'updated' WHERE d.id = $1",
                    opts => opts.Parameter(docId)
                                .PreserveExpiry(true));

                // for server version >= 7.1.0, we expect it to succeed.
                Assert.Empty(result.Errors);
                Assert.Equal<uint?>(1, result.MetaData?.Metrics?.MutationCount);
            }
            catch (CouchbaseException ex)
            {
                // for < 7.1.0, we expect an appropriate error.
                Assert.Contains("Unrecognized parameter in request: preserve_expiry", ex.Message); ;
            }
        }
    }
}
