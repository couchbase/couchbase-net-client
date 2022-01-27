using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using TestOutputLoggerFactory = Couchbase.Transactions.Tests.IntegrationTests.Fixtures.ClusterFixture.TestOutputLoggerFactory;

namespace Couchbase.Transactions.Tests.IntegrationTests
{
    public class MultiTransactionTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public MultiTransactionTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            ClusterFixture.LogLevel = LogLevel.Debug;
            _outputHelper = outputHelper;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(10)]
        public async Task Parallel_Increments_All_Succeed(int parallelCount)
        {
            var barrier = new Barrier(parallelCount);
            var defaultCollection = await _fixture.OpenDefaultCollection(_outputHelper);
            var sampleDoc = new ParallelDoc { Type = nameof(Parallel_Increments_All_Succeed), Name = "ContentiousDoc", Participants = new List<int>(), Count = 0 };
            var docId = nameof(Parallel_Increments_All_Succeed) + Guid.NewGuid().ToString();
            var durability = await TestUtil.InsertAndVerifyDurability(defaultCollection, docId, sampleDoc);

            var sw = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, parallelCount).Select(i => Task.Run(
                async () =>
                {
                    try
                    {
                        _outputHelper.WriteLine($"{sw.Elapsed}:{i}: BEGIN_INIT");
                        var configBuilder = TransactionConfigBuilder.Create();
                        configBuilder.DurabilityLevel(durability);
                        configBuilder.LoggerFactory(new TestOutputLoggerFactory(_outputHelper));
                        if (parallelCount > 50)
                        {
                            configBuilder.ExpirationTime(TimeSpan.FromMinutes(5));
                        }

                        await using var txn = Transactions.Create(_fixture.Cluster, configBuilder.Build());
                        barrier.SignalAndWait();
                        _outputHelper.WriteLine($"{sw.Elapsed}:{i}: BEFORE_RUN");
                        var swRunTime = Stopwatch.StartNew();
                        try
                        {
                            long retryCount = 0;
                            var tr = await txn.RunAsync(async ctx =>
                            {
                                _outputHelper.WriteLine($"{sw.Elapsed}:{i}: BEGIN_RUN, retryCount={retryCount}");
                                Interlocked.Increment(ref retryCount);
                                var getResult = await ctx.GetAsync(defaultCollection, docId);
                                var doc = getResult.ContentAs<ParallelDoc>();
                                doc.Count++;
                                doc.Participants.Add(i);
                                doc.LastParticipant = i;
                                _ = await ctx.ReplaceAsync(getResult, doc);
                                _outputHelper.WriteLine($"{sw.Elapsed}:{i}: END_RUN");
                            });
                        }
                        finally
                        {
                            _outputHelper.WriteLine($"{sw.Elapsed}:{i}: AFTER_RUN {swRunTime.Elapsed}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _outputHelper.WriteLine($"{sw.Elapsed}:{i}:Exception! {ex.ToString()}");
                        throw;
                    }
                }));

            try
            {
                await Task.WhenAll(tasks);
            }
            finally
            {
                var getResult = await defaultCollection.GetAsync(docId);
                var finalDoc = getResult.ContentAs<ParallelDoc>();
                _outputHelper.WriteLine(JObject.FromObject(finalDoc).ToString());
                Assert.Equal(parallelCount, finalDoc.Count);
            }
        }

        private class ParallelDoc
        {
            public string Type { get; set; }
            public string Name { get; set; }
            public List<int> Participants { get; set; } = new List<int>();
            public int Count { get; set; }
            public int LastParticipant { get; set; } = -1;
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
