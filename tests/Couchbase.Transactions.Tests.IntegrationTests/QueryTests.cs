using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.External;
using Couchbase.Core.Exceptions.KeyValue;
using Newtonsoft.Json.Linq;
using Couchbase.Transactions.Config;

namespace Couchbase.Transactions.Tests.IntegrationTests
{
    public class QueryTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public QueryTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Theory]
        [InlineData("SELECT * FROM default WHERE META().id = $docId")]
        [InlineData("DELETE FROM default WHERE META().id = $docId")]
        [InlineData("UPDATE default SET revision = revision + 1 WHERE META().id = $docId")]
        public async Task Singles(string statement)
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var loggerFactory = new ClusterFixture.TestOutputLoggerFactory(_outputHelper);
            await defaultCollection.InsertAsync(docId, sampleDoc);
            var txnCfg = TransactionConfigBuilder.Create().LoggerFactory(loggerFactory);
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var config = new SingleQueryTransactionConfigBuilder();
            config.QueryOptionsValue.Parameter("docId", docId);
            var results = await txn.QueryAsync<object>(statement, config);
            await foreach (var r in results.QueryResult.Rows)
            {
                _outputHelper.WriteLine($"result = {r}");
            }
        }

        [Fact]
        public async Task Single_ParseError()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var statement = "DELETE FROM default WHERE META().id = 'NO_TRAILING_QUOTE";
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            await Assert.ThrowsAsync<TransactionFailedException>(() => txn.QueryAsync<object>(statement));
        }

        [Fact]
        public async Task Single_Delete_DocNotFound()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var statement = "DELETE FROM default WHERE META().id = 'NO_SUCH_DOC_KDJFKDJFKJDAAAJJDFKDJFKJDKJ'";
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            await txn.QueryAsync<object>(statement);
        }

        [Fact]
        public async Task Single_Update_DocNotFound()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var statement = "UPDATE default SET revision = revision + 1 WHERE META().id = 'NO_SUCH_DOC_KDJFKDJFKJDAAAJJDFKDJFKJDKJ'";
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            await txn.QueryAsync<object>(statement);
        }

        [Fact]
        public async Task Single_Insert_DocAlreadyExists()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            await defaultCollection.InsertAsync(docId, sampleDoc, options: new KeyValue.InsertOptions().Durability(KeyValue.DurabilityLevel.MajorityAndPersistToActive));
            var getResult = await defaultCollection.GetAsync(docId);
            Assert.NotNull(getResult);
            _outputHelper.WriteLine($"Blocking Doc = {getResult.ContentAs<object>()}");
            var statement = "INSERT INTO default VALUES ($docId, {\"type\": \"example\" })";
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var t = txn.QueryAsync<object>(statement, config => config.QueryOptionsValue.Parameter("docId", docId));
            var err = await Assert.ThrowsAsync<TransactionFailedException>(() => t);
            Assert.False(err.Result.UnstagingComplete);
            if (err.InnerException is TransactionOperationFailedException tof)
            {
                Assert.False(tof.RetryTransaction);
                Assert.False(tof.AutoRollbackAttempt);
            }
            else if (!(err.InnerException is DocumentExistsException))
            {
                // flaky based on Contains.  May need to change.
                Assert.Contains("document already exists", err.ToString());
            }
        }

        [Fact]
        public async Task LambdaSingle_Insert_DocAlreadyExists()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            await defaultCollection.InsertAsync(docId, sampleDoc, options: new KeyValue.InsertOptions().Durability(KeyValue.DurabilityLevel.MajorityAndPersistToActive));
            var statement = "INSERT INTO default VALUES ($docId, {\"type\": \"example\" })";
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var t = txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                _ = await ctx.QueryAsync<object>((string)statement, opts => opts.Parameter((string)"docId", (object)docId));
            }));
            var err = await Assert.ThrowsAsync<TransactionFailedException>(() => t);
            Assert.False(err.Result.UnstagingComplete);
            if (err.InnerException is TransactionOperationFailedException tof)
            {
                Assert.False(tof.RetryTransaction);
                Assert.False(tof.AutoRollbackAttempt);
            }
            else if (!(err.InnerException is DocumentExistsException))
            {
                // flaky based on Contains.  May need to change.
                Assert.Contains("document already exists", err.ToString());
            }
        }

        [Fact]
        public async Task Mixed_KVInsert_QueryDelete()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                var inserted = await ctx.InsertAsync(defaultCollection, docId, sampleDoc);
                var queryResult = await ctx.QueryAsync<object>((string)"DELETE FROM default WHERE META().id = $docId", opts => opts.Parameter((string)"docId", (object)docId));
            }));

            // verify document was deleted.
            var getCheck = defaultCollection.GetAsync(docId);
            var err = await Assert.ThrowsAsync<DocumentNotFoundException>(() => getCheck);
        }

        [Fact]
        public async Task Mixed_KVInsert_QueryDelete_KVGetOptional()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                var inserted = await ctx.InsertAsync(defaultCollection, docId, sampleDoc);
                var queryResult = await ctx.QueryAsync<object>((string)"DELETE FROM default WHERE META().id = $docId", opts => opts.Parameter((string)"docId", (object)docId));
                var getResult = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.Null(getResult);
            }));

            // verify document was deleted.
            var getCheck = defaultCollection.GetAsync(docId);
            var err = await Assert.ThrowsAsync<DocumentNotFoundException>(() => getCheck);
        }

        [Fact]
        public async Task Mixed_QuerySelect_KVGetOptional()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            await defaultCollection.InsertAsync(docId, sampleDoc, options: new KeyValue.InsertOptions().Durability(KeyValue.DurabilityLevel.MajorityAndPersistToActive));
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                var queryResult = await ctx.QueryAsync<object>((string)"SELECT * FROM default WHERE META().id = $docId", options => options.Parameter((string)"docId", (object)docId));
                var getResult = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.NotNull(getResult);
            }));
        }

        [Fact]
        public async Task Mixed_QuerySelect_KVReplace()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            await defaultCollection.InsertAsync(docId, sampleDoc, options: new KeyValue.InsertOptions().Durability(KeyValue.DurabilityLevel.MajorityAndPersistToActive));
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                var queryResult = await ctx.QueryAsync<object>((string)"SELECT * FROM default WHERE META().id = $docId", options => options.Parameter((string)"docId", (object)docId));
                var newDoc = new { foo = "replaced!" };
                var getResult = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.NotNull(getResult);
                var replacedDoc = await ctx.ReplaceAsync(getResult!, newDoc);
            }));
        }

        [Fact]
        public async Task Mixed_QuerySelect_KVRemove()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            await defaultCollection.InsertAsync(docId, sampleDoc, options: new KeyValue.InsertOptions().Durability(KeyValue.DurabilityLevel.MajorityAndPersistToActive));
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                var queryResult = await ctx.QueryAsync<object>((string)"SELECT * FROM default WHERE META().id = $docId", options => options.Parameter((string)"docId", (object)docId));
                var newDoc = new { foo = "replaced!" };
                var getResult = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.NotNull(getResult);
                await ctx.RemoveAsync(getResult!);
            }));

            // verify document was deleted.
            var getCheck = defaultCollection.GetAsync(docId);
            var err = await Assert.ThrowsAsync<DocumentNotFoundException>(() => getCheck);
        }

        [Fact]
        public async Task Mixed_QuerySelect_KVInsert()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                var queryResult = await ctx.QueryAsync<object>((string)"SELECT * FROM default WHERE META().id = $docId", options => options.Parameter((string)"docId", (object)docId));
                var newDoc = new { foo = "replaced!" };
                var getResult = await ctx.InsertAsync(defaultCollection, docId, sampleDoc);
                Assert.NotNull(getResult);
                var roundTrip = getResult.ContentAs<TestUtil.SampleDoc>();
                Assert.Equal(sampleDoc, roundTrip);
            }));

            // verify document was inserted.
            var getCheck = await defaultCollection.GetAsync(docId);
            Assert.NotNull(getCheck.ContentAs<object>());
        }

        [Fact]
        public async Task Mixed_QuerySelect_KVGet_QueryInsert_KVRemove()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.Majority, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                _ = await ctx.QueryAsync<object>((string)"SELECT 'Hello World' AS Greeting", new TransactionQueryOptions());
                var noDoc = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.Null(noDoc);
                var queryResult = await ctx.QueryAsync<object>((string)"INSERT INTO `default` VALUES ($docId, {\"content\":\"initial\"})", options => options.Parameter((string)"docId", (object)docId));
                var getResult = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.NotNull(getResult);
                await ctx.RemoveAsync(getResult);
            }));

            // verify document was deleted.
            var getCheck = defaultCollection.GetAsync(docId);
            var err = await Assert.ThrowsAsync<DocumentNotFoundException>(() => getCheck);
        }

        [Fact]
        public async Task Mixed_QuerySelect_KVGet_QueryInsert_KVRemove_CasMismatch()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.Majority, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                _ = await ctx.QueryAsync<object>((string)"SELECT 'Hello World' AS Greeting", new TransactionQueryOptions());
                var noDoc = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.Null(noDoc);
                var queryResult = await ctx.QueryAsync<object>((string)"INSERT INTO `default` VALUES ($docId, {\"content\":\"initial\"})", options => options.Parameter((string)"docId", (object)docId));
                var getResult = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.NotNull(getResult);
                await ctx.RemoveAsync(getResult);
            }));

            // verify document was deleted.
            var getCheck = defaultCollection.GetAsync(docId);
            var err = await Assert.ThrowsAsync<DocumentNotFoundException>(() => getCheck);
        }

        [Fact]
        public async Task KVGet_QueryInsert_KVRemove()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            _outputHelper.WriteLine(sampleDoc.ToString());
            var txn = TestUtil.CreateTransaction(_fixture.Cluster, KeyValue.DurabilityLevel.None, _outputHelper);
            var result = await txn.RunAsync((Func<AttemptContext, Task>)(async ctx =>
            {
                var noDoc = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.Null(noDoc);
                var queryResult = await ctx.QueryAsync<object>((string)"INSERT INTO `default` VALUES ($docId, {\"content\":\"initial\"})", options => options.Parameter((string)"docId", (object)docId));
                var getResult = await ctx.GetOptionalAsync(defaultCollection, docId);
                Assert.NotNull(getResult);

                await ctx.RemoveAsync(getResult);
            }));

            // verify document was deleted.
            var getCheck = defaultCollection.GetAsync(docId);
            var err = await Assert.ThrowsAsync<DocumentNotFoundException>(() => getCheck);
        }
    }
}
