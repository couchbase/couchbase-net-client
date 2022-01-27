#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.DataModel;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.External;
using Couchbase.Transactions.Error.Internal;
using Couchbase.Transactions.Internal.Test;
using Couchbase.Transactions.Support;
using Couchbase.Transactions.Tests.IntegrationTests.Errors;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using RemoveOptions = Couchbase.KeyValue.RemoveOptions;

namespace Couchbase.Transactions.Tests.IntegrationTests
{
    /// <summary>
    /// Tests written independently of the java or fit performer test suites.
    /// </summary>
    public class TransactionsTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public TransactionsTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            ClusterFixture.LogLevel = LogLevel.Debug;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Basic_Insert_Should_Succeed()
        {
            // Use a feature from an unreleased CouchbaseNetClient to guarantee we're using latest from master instead.
            var ex = new Core.Exceptions.CasMismatchException();

            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Basic_Insert_Should_Succeed), foo = "bar", revision = 100 };
            var docId = nameof(Basic_Insert_Should_Succeed) + Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId + "_testDurability", sampleDoc);

                var txn = Transactions.Create(_fixture.Cluster);
                var configBuilder = TransactionConfigBuilder.Create();
                configBuilder.DurabilityLevel(durability);

                var result = await txn.RunAsync(async ctx =>
                {
                    var insertResult = await ctx.InsertAsync(defaultCollection, docId, sampleDoc).ConfigureAwait(false);
                    Assert.Equal("_default", insertResult?.TransactionXattrs?.AtrRef?.CollectionName);
                    Assert.Equal("_default", insertResult?.TransactionXattrs?.AtrRef?.ScopeName);

                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    Assert.NotNull(getResult);
                    var asJobj = getResult!.ContentAs<JObject>();
                    Assert.Equal("bar", asJobj["foo"].Value<string>());
                });

                var postTxnGetResult = await defaultCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<dynamic>();
                Assert.Equal("100", postTxnDoc.revision.ToString());

                var postTxnLookupInResult =
                    await defaultCollection.LookupInAsync(docId, spec => spec.Get("txn", isXattr: true));
                Assert.False(postTxnLookupInResult.Exists(0));
            }
            catch
            {
                throw;
            }
            finally
            {
                try
                {
                    await defaultCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                }
            }
        }

        [Fact]
        public async Task Basic_Insert_CustomCollection()
        {
            // Use a feature from an unreleased CouchbaseNetClient to guarantee we're using latest from master instead.
            var ex = new Core.Exceptions.CasMismatchException();

            var customCollection = await _fixture.OpenCustomCollection(_outputHelper);
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Basic_Insert_Should_Succeed), foo = "bar", revision = 100 };
            var docId = nameof(Basic_Insert_Should_Succeed) + Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(customCollection, docId + "_testDurability", sampleDoc);

                var txn = Transactions.Create(_fixture.Cluster);
                var configBuilder = TransactionConfigBuilder.Create();
                configBuilder.DurabilityLevel(durability);

                var result = await txn.RunAsync(async ctx =>
                {
                    var insertResult = await ctx.InsertAsync(customCollection, docId, sampleDoc).ConfigureAwait(false);
                    Assert.Equal("_default", insertResult?.TransactionXattrs?.AtrRef?.CollectionName);
                    Assert.Equal("_default", insertResult?.TransactionXattrs?.AtrRef?.ScopeName);
                    var getResult = await ctx.GetAsync(customCollection, docId);
                    Assert.NotNull(getResult);
                    var asJobj = getResult!.ContentAs<JObject>();
                    Assert.Equal("bar", asJobj["foo"].Value<string>());
                });

                var postTxnGetResult = await customCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<dynamic>();
                Assert.Equal("100", postTxnDoc.revision.ToString());

                var postTxnLookupInResult =
                    await customCollection.LookupInAsync(docId, spec => spec.Get("txn", isXattr: true));
                Assert.False(postTxnLookupInResult.Exists(0));
            }
            catch
            {
                throw;
            }
            finally
            {
                try
                {
                    await customCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                }
            }
        }

        [Fact]
        public async Task Basic_Insert_Should_Succeed_CustomMetadataCollection()
        {
            // Use a feature from an unreleased CouchbaseNetClient to guarantee we're using latest from master instead.
            var ex = new Core.Exceptions.CasMismatchException();

            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var customCollection = await _fixture.OpenCustomCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Basic_Insert_Should_Succeed), foo = "bar", revision = 100 };
            var docId = nameof(Basic_Insert_Should_Succeed) + Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId + "_testDurability", sampleDoc);

                var configBuilder = TransactionConfigBuilder.Create();
                configBuilder.DurabilityLevel(durability);
                configBuilder.MetadataCollection(customCollection);
                var txn = Transactions.Create(_fixture.Cluster, configBuilder.Build());

                var result = await txn.RunAsync(async ctx =>
                {
                    var insertResult = await ctx.InsertAsync(defaultCollection, docId, sampleDoc).ConfigureAwait(false);
                    Assert.Equal(ClusterFixture.CustomCollectionName, insertResult?.TransactionXattrs?.AtrRef?.CollectionName);

                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    Assert.NotNull(getResult);
                    var asJobj = getResult!.ContentAs<JObject>();
                    Assert.Equal("bar", asJobj["foo"].Value<string>());
                    Assert.Equal(ClusterFixture.CustomCollectionName, getResult?.TransactionXattrs?.AtrRef?.CollectionName);
                });

                var postTxnGetResult = await defaultCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<dynamic>();
                Assert.Equal("100", postTxnDoc.revision.ToString());

                var postTxnLookupInResult =
                    await defaultCollection.LookupInAsync(docId, spec => spec.Get("txn", isXattr: true));
                Assert.False(postTxnLookupInResult.Exists(0));
            }
            catch
            {
                throw;
            }
            finally
            {
                try
                {
                    await defaultCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                }
            }
        }


        [Fact]
        public async Task Basic_Replace_Should_Succeed()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new {type = nameof(Basic_Replace_Should_Succeed), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);

                var txn = Transactions.Create(_fixture.Cluster);
                var configBuilder = TransactionConfigBuilder.Create();
                configBuilder.DurabilityLevel(durability);

                var result = await txn.RunAsync(async ctx =>
                {
                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    var docGet = getResult.ContentAs<dynamic>();

                    docGet.revision = docGet.revision + 1;
                    var replaceResult = await ctx.ReplaceAsync(getResult, docGet);
                });

                Assert.True(result.UnstagingComplete);

                var postTxnGetResult = await defaultCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<dynamic>();
                Assert.Equal("101", postTxnDoc.revision.ToString());

                await txn.DisposeAsync();
            }
            finally
            {
                try
                {
                    await defaultCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                    throw;
                }
            }
        }

        [Fact]
        public async Task Two_Replaces_Same_Document()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Basic_Replace_Should_Succeed), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);
                var txn = TestUtil.CreateTransaction(_fixture.Cluster, durability, _outputHelper);

                var result = await txn.RunAsync(async ctx =>
                {
                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    Assert.NotNull(getResult);
                    var docGet = getResult!.ContentAs<dynamic>();

                    docGet.revision = docGet.revision + 1;
                    var replaceResult = await ctx.ReplaceAsync(getResult, docGet);

                    var replacedDoc = replaceResult.ContentAs<dynamic>();
                    replacedDoc.foo = "replaced_foo";
                    var secondReplaceResult = await ctx.ReplaceAsync(replaceResult, replacedDoc);
                });

                var postTxnGetResult = await defaultCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<JObject>();
                Assert.Equal(101, postTxnDoc["revision"].Value<int>());
                Assert.Equal("replaced_foo", postTxnDoc["foo"].Value<string>());

                await txn.DisposeAsync();
            }
            finally
            {
                try
                {
                    await defaultCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                    throw;
                }
            }
        }


        [Fact]
        public async Task Basic_Remove_Should_Succeed()
        {
            bool removed = false;
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Basic_Remove_Should_Succeed), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);

                var txn = Transactions.Create(_fixture.Cluster);
                var configBuilder = TransactionConfigBuilder.Create();
                configBuilder.DurabilityLevel(durability);

                var result = await txn.RunAsync(async ctx =>
                {
                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    var docGet = getResult.ContentAs<dynamic>();

                    docGet.revision = docGet.revision + 1;
                    await ctx.RemoveAsync(getResult);
                });

                await Assert.ThrowsAsync<DocumentNotFoundException>(() => defaultCollection.GetAsync(docId));
                removed = true;
            }
            finally
            {
                try
                {
                    if (!removed)
                    {
                        await defaultCollection.RemoveAsync(docId);
                    }
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                    throw;
                }
            }
        }

        [Fact]
        public async Task Basic_Rollback_Should_Result_In_No_Changes()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Basic_Rollback_Should_Result_In_No_Changes), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);
                var configBuilder = DefaultConfigBuilder(_outputHelper);
                configBuilder.DurabilityLevel(durability);

                var txn = Transactions.Create(_fixture.Cluster, configBuilder);

                var result = await txn.RunAsync(async ctx =>
                {
                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    var docGet = getResult.ContentAs<dynamic>();

                    docGet.revision = docGet.revision + 1;
                    var replaceResult = await ctx.ReplaceAsync(getResult, docGet);
                    await ctx.RollbackAsync();
                });

                var postTxnGetResult = await defaultCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<dynamic>();
                Assert.Equal("100", postTxnDoc.revision.ToString());
            }
            finally
            {
                try
                {
                    await defaultCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                    throw;
                }
            }
        }

        [Fact]
        public async Task Rollback_Insert_Should_Result_In_No_Document()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Rollback_Insert_Should_Result_In_No_Document), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();
            var durability = DurabilityLevel.None;

            var txn = Transactions.Create(_fixture.Cluster);
            var configBuilder = TransactionConfigBuilder.Create();
            configBuilder.DurabilityLevel(durability);

            var runTask = txn.RunAsync(async ctx =>
            {
                var insertResult = await ctx.InsertAsync(defaultCollection, docId, sampleDoc);
                throw ErrorClass.FailHard.Throwable();
            });

            var transactionFailedException = await Assert.ThrowsAsync<TransactionFailedException>(() => runTask);
            var result = transactionFailedException.Result;
            Assert.False(result.UnstagingComplete);

            var postTxnGetTask = defaultCollection.GetAsync(docId);
            _ = await Assert.ThrowsAsync<DocumentNotFoundException>(() => postTxnGetTask);
        }

        [Fact]
        public async Task Exception_Rollback_Should_Result_In_No_Changes()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Exception_Rollback_Should_Result_In_No_Changes), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();
            try
            {
                var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);

                var txn = Transactions.Create(_fixture.Cluster);
                var configBuilder = TransactionConfigBuilder.Create();
                configBuilder.DurabilityLevel(durability);

                int attemptCount = 0;
                var runTask = txn.RunAsync(async ctx =>
                {
                    attemptCount++;
                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    var docGet = getResult.ContentAs<dynamic>();

                    docGet.revision = docGet.revision + 1;
                    var replaceResult = await ctx.ReplaceAsync(getResult, docGet);
                    throw new InvalidOperationException("Forcing rollback.");
                });

                await Assert.ThrowsAsync<TransactionFailedException>(() => runTask);
                Assert.Equal(1, attemptCount);

                var postTxnGetResult = await defaultCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<dynamic>();
                Assert.Equal("100", postTxnDoc.revision.ToString());
            }
            finally
            {
                try
                {
                    await defaultCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                    throw;
                }
            }
        }

        [Fact]
        public async Task Retry_On_Certain_Failures()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(Retry_On_Certain_Failures), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();
            try
            {
                var durability = DurabilityLevel.Majority;

                try
                {
                    _ = await defaultCollection.InsertAsync(docId, sampleDoc, opts => opts.Durability(durability));
                }
                catch (DurabilityImpossibleException)
                {
                    // when running on single-node cluster, such as localhost.
                    durability = DurabilityLevel.None;
                    _ = await defaultCollection.InsertAsync(docId, sampleDoc, opts => opts.Durability(durability));
                }

                var txn = Transactions.Create(_fixture.Cluster);
                var configBuilder = TransactionConfigBuilder.Create();
                configBuilder.DurabilityLevel(durability);

                int attemptCount = 0;
                var result =await txn.RunAsync(async ctx =>
                {
                    attemptCount++;
                    var getResult = await ctx.GetAsync(defaultCollection, docId);
                    var docGet = getResult.ContentAs<dynamic>();

                    docGet.revision = docGet.revision + 1;
                    var replaceResult = await ctx.ReplaceAsync(getResult, docGet);
                    if (attemptCount < 3)
                    {
                        throw new TestRetryException("force retry", new InvalidOperationException());
                    }
                });

                Assert.True(result.UnstagingComplete);

                var postTxnGetResult = await defaultCollection.GetAsync(docId);
                var postTxnDoc = postTxnGetResult.ContentAs<dynamic>();
                Assert.Equal("101", postTxnDoc.revision.ToString());
            }
            catch (Exception ex)
            {
                _outputHelper.WriteLine($"Error during main try: {ex.ToString()}");
                throw;
            }
            finally
            {
                try
                {
                    await defaultCollection.RemoveAsync(docId);
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Error during cleanup: {e.ToString()}");
                    throw;
                }
            }
        }
        private class TestRetryException : Exception, IRetryable
        {
            public TestRetryException(string? message, Exception? inner)
                : base(message, inner)
            {
            }
        }

        [Fact]
        public async Task Get_Repeated_Failures_Should_Throw_TransactionFailed()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var docId = Guid.NewGuid().ToString();

            var txn = Transactions.Create(_fixture.Cluster);
            int attempts = 0;
            txn.TestHooks = new DelegateTestHooks()
            {
                BeforeDocGetImpl = (ctx, id) => throw new InternalIntegrationTestException()
                    {
                        CausingErrorClass = attempts++ < 5 ? ErrorClass.FailTransient : ErrorClass.FailOther
                    }
            };

            var runTask = txn.RunAsync(async ctx =>
            {
                var getResult = await ctx.GetAsync(defaultCollection, docId);
                var docGet = getResult?.ContentAs<dynamic>();
                Assert.False(true, "Should never have reached here.");
            });

            var transactionFailedException = await Assert.ThrowsAsync<TransactionFailedException>(() => runTask);
            Assert.NotNull(transactionFailedException.Result);
            Assert.False(transactionFailedException.Result.UnstagingComplete);
        }

        [Fact]
        public async Task Get_NotFound_Throws_DocumentNotFound()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var docId = Guid.NewGuid().ToString();

            var txn = Transactions.Create(_fixture.Cluster);

            var runTask = txn.RunAsync(async ctx =>
            {
                var getResult = await ctx.GetAsync(defaultCollection, docId);
                var docGet = getResult?.ContentAs<dynamic>();
                Assert.False(true, "Should never have reached here.");
            });

            var transactionFailedException = await Assert.ThrowsAsync<TransactionFailedException>(() => runTask);
            Assert.NotNull(transactionFailedException.Result);
            Assert.False(transactionFailedException.Result.UnstagingComplete);
        }


        [Fact]
        public async Task DocumentLookup_Should_Include_Metadata()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(DocumentLookup_Should_Include_Metadata), foo = "bar", revision = 100 };
            var docId = Guid.NewGuid().ToString();

            var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);

            var configBuilder = TransactionConfigBuilder.Create();
            configBuilder.DurabilityLevel(durability);
            if (Debugger.IsAttached)
            {
                configBuilder.ExpirationTime(TimeSpan.FromMinutes(10));
            }

            var txn = Transactions.Create(_fixture.Cluster, configBuilder);


            txn.TestHooks = new DelegateTestHooks()
            {
                BeforeDocCommittedImpl = async (ctx, id) =>
                {
                    var documentLookupResult =
                        await DocumentRepository.LookupDocumentAsync(defaultCollection, id, null, true);

                    return 0;
                },

                AfterStagedReplaceCompleteImpl = async (ctx, id) =>
                {
                    var documentLookupResult =
                        await DocumentRepository.LookupDocumentAsync(defaultCollection, id, null, true);

                    return 0;
                }
            };

            var result = await txn.RunAsync(async ctx =>
            {
                var getResult = await ctx.GetAsync(defaultCollection, docId);
                var docGet = getResult!.ContentAs<dynamic>();

                docGet.revision = docGet.revision + 1;
                var replaceResult = await ctx.ReplaceAsync(getResult, docGet);

                var documentLookupResult =
                    await DocumentRepository.LookupDocumentAsync(defaultCollection, docId, null, true);

                Assert.NotNull(documentLookupResult?.TransactionXattrs);
                Assert.NotNull(documentLookupResult?.StagedContent?.ContentAs<object>());
                _outputHelper.WriteLine(JObject.FromObject(documentLookupResult!.TransactionXattrs).ToString());
            });
        }

        [Fact]
        public async Task DocumentLookup_Basic()
        {
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new { type = nameof(DocumentLookup_Basic), foo = "bar", revision = 100, sub = new { a = 1, b = 2 } };
            var docId = Guid.NewGuid().ToString();
            var insertResult =
                await defaultCollection.InsertAsync(docId, sampleDoc, opts => opts.Durability(DurabilityLevel.None));
            var mutateResult =
                await defaultCollection.MutateInAsync(docId, specs =>
                        specs.Upsert("txn.id.txn", "tid1", createPath: true, isXattr: true)
                            .Upsert("txn.id.atmpt", "atmptid1", createPath: true, isXattr: true),
                    opts => opts.CreateAsDeleted(true)
                        .Cas(insertResult.Cas)
                        .Durability(DurabilityLevel.None)
                        .StoreSemantics(StoreSemantics.Replace));

            var docLookup = await DocumentRepository.LookupDocumentAsync(defaultCollection, docId, null, true);
            Assert.NotNull(docLookup.TransactionXattrs);
        }

        private TransactionConfigBuilder DefaultConfigBuilder(ITestOutputHelper outputHelper)
        {
            return TransactionConfigBuilder.Create()
                .CleanupClientAttempts(false)
                .CleanupLostAttempts(false)
                .LoggerFactory(new ClusterFixture.TestOutputLoggerFactory(outputHelper));
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
