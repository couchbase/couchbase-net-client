using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Transactions.Cleanup.LostTransactions;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Transactions.Tests.IntegrationTests.Cleanup
{
    public class LostTransactionTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public LostTransactionTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            ClusterFixture.LogLevel = LogLevel.Debug;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task RunsWithoutException()
        {
            var loggerFactory = new ClusterFixture.TestOutputLoggerFactory(_outputHelper);
            var manager = new LostTransactionManager(
                cluster: _fixture.Cluster,
                loggerFactory: loggerFactory,
                cleanupWindow: TimeSpan.FromMilliseconds(1_000),
                keyValueTimeout: null);
            try
            {
                await Task.Delay(1_500);
                Assert.NotEqual(0, manager.DiscoveredBucketCount);
                Assert.NotEqual(0, manager.RunningCount);
                await Task.Delay(TimeSpan.FromMilliseconds(10_000));
                Assert.NotEqual(0, manager.TotalRunCount);
            }
            finally
            {
                await manager.DisposeAsync();
                await Task.Delay(100);
                Assert.Equal(0, manager.RunningCount);
            }
        }

        [Fact(Skip = "Skipped by default")]
        public async Task Nuke_All_Client_Records()
        {
            // this is not a test, just a utility method to reset the data.
            var loggerFactory = new ClusterFixture.TestOutputLoggerFactory(_outputHelper);
            var cluster = _fixture.Cluster;
            var buckets = await cluster.Buckets.GetAllBucketsAsync();
            foreach (var bucket in buckets)
            {
                var bkt = await cluster.BucketAsync(bucket.Key);
                var col = await bkt.DefaultCollectionAsync();
                _outputHelper.WriteLine($"Removing client record on {bucket.Key}");
                try
                {
                    await col.RemoveAsync("_txn:client-record");
                }
                catch (DocumentNotFoundException)
                {
                    _outputHelper.WriteLine("(no client record found on this bucket)");
                }
            }
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