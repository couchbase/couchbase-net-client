using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.External;
using Couchbase.Transactions.Internal;
using Couchbase.Transactions.Tests.UnitTests.Mocks;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Transactions.Tests.UnitTests
{
    public class TransactionsTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public TransactionsTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Canonical_Example_Compiles()
        {
            try
            {
                await CanonicalExample();
            }
            catch (Exception e)
            {
                _outputHelper.WriteLine($"{nameof(Canonical_Example_Compiles)}: Unhandled Exception: {e.ToString()}");
            }
        }

        [Fact]
        public async Task DataAccess_Is_Abstracted()
        {
            // After the data access was refactored into repository classes, the ICouchbaseCollection instances passed in shouldn't actually be accessed.
            // We verify this by using Mock with strict behavior and NotImplemented members.
            // The test should run to the end without hitting any of the ICouchbaseCollection other than the names and fetching the default collection on the bucket.
            using var cluster = CreateTestCluster(Enumerable.Empty<TransactionGetResult>());
            var mockCollection = new MockCollectionWithNames(nameof(DataAccess_Is_Abstracted) + "col", nameof(DataAccess_Is_Abstracted) + "scp", nameof(DataAccess_Is_Abstracted) + "bkt");
            var atr = new MockAtrRepository();
            var doc = new MockDocumentRepository();
            string docId = nameof(DataAccess_Is_Abstracted) + ".id";
            var mockLookupInResult = new Mock<ILookupInResult>(MockBehavior.Strict);
            mockLookupInResult.SetupGet(l => l.IsDeleted).Returns(false);
            mockLookupInResult.SetupGet(l => l.Cas).Returns(5);
            doc.Add(mockCollection, docId, new DataModel.DocumentLookupResult(docId, new JObjectContentWrapper(new { foo = "original" }), null, mockLookupInResult.Object, new Components.DocumentMetadata(), mockCollection));
            var configBuilder = TransactionConfigBuilder.Create().DurabilityLevel(DurabilityLevel.Majority);
            try
            {
                await using var transactions = Transactions.Create(cluster);
                transactions.DocumentRepository = doc;
                transactions.AtrRepository = atr;
                var tr = await transactions.RunAsync(async ctx =>
                {
                    var fetched = await ctx.GetAsync(mockCollection, docId);
                    var replaced = await ctx.ReplaceAsync(fetched, new { foo = "bar" });
                    await ctx.RemoveAsync(replaced);
                    var inserted = await ctx.InsertAsync(mockCollection, docId + "inserted", new { foo = "inserted in transaction" });
                });

                try
                {
                    var aborted = await transactions.RunAsync(async ctx =>
                    {
                        var inserted = await ctx.InsertAsync(mockCollection, docId + "inserted_to_rollback", new { foo = "to be thrown" });
                        throw new InvalidOperationException("force fail");
                    });
                }
                catch (TransactionFailedException)
                {
                }
            }
            catch (Exception ex)
            {
                _outputHelper.WriteLine(ex.ToString());
                throw;
            }
        }

        private async Task<bool> CanonicalExample()
        {
            using var cluster = CreateTestCluster(Enumerable.Empty<TransactionGetResult>());

            var configBuilder = TransactionConfigBuilder.Create()
                .DurabilityLevel(DurabilityLevel.Majority);

            if (Debugger.IsAttached)
            {
                configBuilder.ExpirationTime(TimeSpan.FromMinutes(10));
            }

            using var transactions = Transactions.Create(cluster, configBuilder.Build());

            bool reachedPostCommit = false;
            TransactionResult tr = null;
            try
            {
                tr = await transactions.RunAsync(async ctx =>
                {
                    // Inserting a doc:
                    var docId = "test-id";
                    var bucket = await cluster.BucketAsync("test-bucket").ConfigureAwait(false);
                    var collection = await bucket.DefaultCollectionAsync();
                    var insertResult = await ctx.InsertAsync(collection, docId, new JObject()).ConfigureAwait(false);

                    // Getting documents:
                    var docOpt = await ctx.GetOptionalAsync(collection, docId).ConfigureAwait(false);
                    var doc = await ctx.GetAsync(collection, docId).ConfigureAwait(false);

                    // Replacing a document:
                    var anotherDoc = await ctx.GetAsync(collection, "anotherDoc").ConfigureAwait(false);
                    var content = anotherDoc.ContentAs<JObject>();
                    content["transactions"] = "are awesome";
                    await ctx.ReplaceAsync(anotherDoc, content);

                    // Removing a document:
                    var yetAnotherDoc = await ctx.GetAsync(collection, "yetAnotherDoc)").ConfigureAwait(false);
                    await ctx.RemoveAsync(yetAnotherDoc).ConfigureAwait(false);

                    await ctx.CommitAsync().ConfigureAwait(false);
                    reachedPostCommit = true;
                });
            }
            catch (TransactionCommitAmbiguousException e)
            {
                // TODO: log individual errors
                _outputHelper.WriteLine(e.ToString());
                throw;
            }
            catch (TransactionFailedException e)
            {
                // TODO: log errors from exception
                _outputHelper.WriteLine(e.ToString());
                throw;
            }

            Assert.NotNull(tr);
            return reachedPostCommit;
        }

        private ICluster CreateTestCluster(IEnumerable<TransactionGetResult> mockDocs)
        {
            var mockCollection = new MockCollection(mockDocs);
            var mockBucket = new Mock<IBucket>(MockBehavior.Strict);
            mockBucket.SetupGet(b => b.Name).Returns("MockBucket");
            mockBucket.Setup(b => b.DefaultCollectionAsync())
                .Returns(new ValueTask<ICouchbaseCollection>(mockCollection));
            mockBucket.Setup(b => b.DefaultCollection())
                .Returns(mockCollection);
            var mockScope = new Mock<IScope>(MockBehavior.Strict);
            mockScope.SetupGet(s => s.Name).Returns("MockScope");
            mockScope.SetupGet(s => s.Bucket).Returns(mockBucket.Object);
            mockCollection.Scope = mockScope.Object;
            var mockCluster = new Mock<ICluster>(MockBehavior.Strict);
            mockCluster.Setup(c => c.BucketAsync(It.IsAny<string>()))
                .ReturnsAsync(mockBucket.Object);
            mockCluster.Setup(c => c.Dispose());
            mockCluster.Setup(c => c.DisposeAsync()).Returns(new ValueTask());
            mockCluster.SetupGet(c => c.ClusterServices).Returns(new MockClusterServices());
            return mockCluster.Object;
        }
    }
}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
