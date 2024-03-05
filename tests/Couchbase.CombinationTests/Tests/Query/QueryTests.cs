using Couchbase.KeyValue;
using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.Retry;
using Couchbase.Query;
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
        public async Task Test_Custom_DoNotRetryPreparedStatementRetryStrategy()
        {
            var statement = "EXECUTE prepared2;";
            await _fixture.BuildAsync();

            //prepared statement does  not exist, but we want to fail fast only in this case
            await Assert.ThrowsAsync<PreparedStatementException>(async () => await _fixture.Cluster.QueryAsync<dynamic>(statement,
                options => options.RetryStrategy(new DoNotRetryPreparedStatementRetryStrategy())));

        }

        /// <summary>
        /// An example of a custom RetryStrategy which throws immediately when QueryPreparedStatementFailure
        /// so that the caller can immediately recreate the prepared statement and incur the overhead of the
        /// retries which will never succeed until it times out or reaches the max # of retries.
        /// </summary>
        private class DoNotRetryPreparedStatementRetryStrategy : BestEffortRetryStrategy
        {
            public override RetryAction RetryAfter(IRequest request, RetryReason reason)
            {
                //do not retry prepared statements, but fast fail instead
                if (reason == RetryReason.QueryPreparedStatementFailure)
                {
                    return RetryAction.Duration(null);
                }
                return base.RetryAfter(request, reason);
            }
        }

        [Fact]
        public async Task Test_SingleQoutes()
        {
            var statement = "select d.* from default as d where d.adStrNa == \"15TH TEST\'WEV\"";
            await _fixture.BuildAsync();
            var result = await _fixture.Cluster.QueryAsync<dynamic>(statement);

            var value = await result.FirstAsync();
            Assert.NotNull(value);
        }

        [Fact]
        public async Task Test_SingleQoutes2()
        {
            await _fixture.BuildAsync();

            var statement = "select d.* from default as d where d.adStrNa == $1";
            var result = await _fixture.Cluster.QueryAsync<dynamic>(statement, new QueryOptions().Parameter("15TH TEST'WEV"));

            var value = await result.FirstAsync();
            Assert.NotNull(value);
        }

        [Fact]
        public async Task Test_SingleQoutes_NamedParameters()
        {
            await _fixture.BuildAsync();

            var statement = "select d.* from default as d where d.adStrNa == $test";
            var result = await _fixture.Cluster.QueryAsync<dynamic>(statement, new QueryOptions().Parameter("test", "15TH TEST'WEV"));

            var value = await result.FirstAsync();
            Assert.NotNull(value);
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
        [Fact]
        public async Task Task_InternalServerFailureException_Contains_Message()
        {
            await _fixture.BuildAsync();
            var cluster = _fixture.Cluster;

            try
            {
                var result = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` WHERE type=$name;",
                    parameter => { }).ConfigureAwait(false);
                Assert.False(true, "Exception should have been thrown.");
            }
            catch (InternalServerFailureException e)
            {
                Assert.Equal(
                    "Error evaluating filter - cause: No value for named parameter $name (near line 1, column 43). [5010]",
                    e.Message);
            }
        }
    }
}
