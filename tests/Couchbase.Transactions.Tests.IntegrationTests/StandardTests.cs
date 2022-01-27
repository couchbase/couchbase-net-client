using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.DataModel;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Internal.Test;
using Couchbase.Transactions.Tests.IntegrationTests.Errors;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
// ReSharper disable InconsistentNaming

namespace Couchbase.Transactions.Tests.IntegrationTests
{
    /// <summary>
    /// Tests written to mirror tests in StandardTest.java from the txn-driver project.
    /// </summary>
    public class StandardTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public StandardTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            ClusterFixture.LogLevel = LogLevel.Debug;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task getDocFailsRepeatedly()
        {
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);
            using var cluster = await _fixture.OpenClusterAsync(_outputHelper);
            var txn = TestUtil.CreateTransaction(cluster, durability, _outputHelper);

            int attempts = 0;
            txn.TestHooks = new DelegateTestHooks()
            {
                BeforeDocGetImpl = (ctx, id) =>
                {
                    if (attempts++ < 2)
                    {
                        _outputHelper.WriteLine(nameof(ITestHooks.BeforeDocGet) + " fail " + attempts);
                        throw ErrorClass.FailTransient.Throwable();
                    }

                    _outputHelper.WriteLine(nameof(ITestHooks.BeforeDocGet) + " pass after " + attempts);
                    return Task.FromResult<int?>(2);
                }
            };

            bool getSucceeded = false;
            var result = await txn.RunAsync(async ctx =>
            {
                var getResult = await ctx.GetAsync(defaultCollection, docId);
                var docGet = getResult?.ContentAs<dynamic>();
                getSucceeded = true;
            });

            Assert.NotNull(result);
            Assert.True(getSucceeded);
            Assert.NotEqual(0, attempts);
        }

        [Fact]
        public async Task removeStagesBackupMetadata()
        {
            /*
             *     public void removeStagesBackupMetadata() {
               String docId = TestUtils.docId(collection, 0);
               JsonObject initial = JsonObject.create().put(Strings.CONTENT_NAME, INITIAL_CONTENT_VALUE);
               collection.insert(docId, initial);

               TransactionBuilder.create(shared)
               .failHard(HookPoint.BEFORE_ATR_COMMIT)
               .remove(docId)
               .sendToPerformer();

               DocValidator.assertRemovedDocIsStaged(collection,docId);
               }
             */
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);
            await using var txn = TestUtil.CreateTransaction(_fixture.Cluster, durability, _outputHelper);
            txn.TestHooks = new DelegateTestHooks()
            {
                BeforeAtrCommitImpl = (ctx) => throw ErrorClass.FailHard.Throwable()
            };

            try
            {
                var runTask = txn.RunAsync(async ctx =>
                {
                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    Assert.NotNull(getResult);
                    await ctx.RemoveAsync(getResult!);
                });

                var err = await Assert.ThrowsAsync<TransactionFailedException>(async () =>
                {
                    await runTask;
                });

                var docLookup = await DocumentRepository.LookupDocumentAsync(defaultCollection, docId, null, true);
                Assert.NotNull(docLookup?.TransactionXattrs?.RestoreMetadata);
                Assert.Equal("remove", docLookup?.TransactionXattrs?.Operation?.Type);
                Assert.Null(docLookup?.TransactionXattrs?.Operation?.StagedDocument);
            }
            finally
            {
            }
        }

        [Fact]
        public async Task updateStagesBackupMetadata()
        {
            /*     public void updateStagesBackupMetadata() {
               String docId = TestUtils.docId(collection, 0);
               JsonObject initial = JsonObject.create().put(Strings.CONTENT_NAME, INITIAL_CONTENT_VALUE);
               JsonObject after = JsonObject.create().put(Strings.CONTENT_NAME, UPDATED_CONTENT_VALUE);
               collection.insert(docId, initial);

               TransactionBuilder.create(shared)
               .failHard(HookPoint.BEFORE_ATR_COMMIT)
               .replace(docId,after)
               .sendToPerformer();

               DocValidator.assertReplacedDocIsStagedAndContentEquals(collection,docId,initial);
               }
            */
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);
            await using var txn = TestUtil.CreateTransaction(_fixture.Cluster, durability, _outputHelper);
            txn.TestHooks = new DelegateTestHooks()
            {
                BeforeAtrCommitImpl = (ctx) => throw ErrorClass.FailHard.Throwable()
            };

            var runTask = txn.RunAsync(async ctx =>
            {
                var getResult = await ctx.GetAsync(defaultCollection, docId);
                Assert.NotNull(getResult);
                var jobj = getResult.ContentAs<JObject>();
                jobj["newfield"] = "newval";
                await ctx.ReplaceAsync(getResult!, jobj);
            });

            var err = await Assert.ThrowsAsync<TransactionFailedException>(async () =>
            {
                await runTask;
            });

            var docLookup = await DocumentRepository.LookupDocumentAsync(defaultCollection, docId, null, true);
            Assert.NotNull(docLookup?.TransactionXattrs?.RestoreMetadata);
            Assert.Equal("replace", docLookup?.TransactionXattrs?.Operation?.Type);
            Assert.NotNull(docLookup?.TransactionXattrs?.Operation?.StagedDocument);
        }

        [Fact]
        public async Task insertStagesBackupMetadata()
        {
            // NOTE: test is misnamed.  Insert does *not* stage "restore" metadata.
            /*
             *         String docId = TestUtils.docId(collection, 0);
               JsonObject initial = JsonObject.create().put(Strings.CONTENT_NAME, INITIAL_CONTENT_VALUE);

               TransactionBuilder.create(shared)
               .failHard(HookPoint.BEFORE_ATR_COMMIT)
               .insert(docId,initial)
               .sendToPerformer();

               DocValidator.assertInsertedDocIsStaged(shared, collection,docId);
             */
            (var defaultCollection, var docId, var sampleDoc) = await TestUtil.PrepSampleDoc(_fixture, _outputHelper);
            var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId + "_testDurability", sampleDoc);
            await using var txn = TestUtil.CreateTransaction(_fixture.Cluster, durability, _outputHelper);
            txn.TestHooks = new DelegateTestHooks()
            {
                BeforeAtrCommitImpl = (ctx) => throw ErrorClass.FailHard.Throwable()
            };


            var runTask = txn.RunAsync(async ctx =>
            {
                var insertResult = await ctx.InsertAsync(defaultCollection, docId, sampleDoc);
            });

            var err = await Assert.ThrowsAsync<TransactionFailedException>(async () =>
            {
                await runTask;
            });

            var docLookup = await DocumentRepository.LookupDocumentAsync(defaultCollection, docId, null, fullDocument: false);
            Assert.True(docLookup.IsDeleted);
            Assert.Equal("insert", docLookup?.TransactionXattrs?.Operation?.Type);
            Assert.Null(docLookup?.TransactionXattrs?.RestoreMetadata);
            Assert.NotNull(docLookup?.TransactionXattrs?.Operation?.StagedDocument);
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
