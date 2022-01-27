using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Transactions.Cleanup;
using Couchbase.Transactions.Cleanup.LostTransactions;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Transactions.Tests.IntegrationTests.Cleanup
{
    public class PerBucketCleanerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public PerBucketCleanerTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            ClusterFixture.LogLevel = LogLevel.Debug;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task DefaultBucket_BasicBackgroundRun()
        {
            var loggerFactory = new ClusterFixture.TestOutputLoggerFactory(_outputHelper);
            var clientUuid = Guid.NewGuid().ToString();
            var cleaner = new Cleaner(_fixture.Cluster, null, loggerFactory);
            var collection = await _fixture.OpenDefaultCollection(_outputHelper);
            var repo = new CleanerRepository(collection, null);
            PerBucketCleaner perBucketCleaner = null;
            try
            {
                perBucketCleaner = new PerBucketCleaner(clientUuid, cleaner, repo, TimeSpan.FromSeconds(0.1), loggerFactory);
                await Task.Delay(500);
                Assert.True(perBucketCleaner.Running);
                for (var i = 0; i < 10 && perBucketCleaner.RunCount < 1; i++)
                {
                    await Task.Delay(500);
                }

                Assert.NotEqual(0, perBucketCleaner.RunCount);
            }
            finally
            {
                await perBucketCleaner.DisposeAsync();
            }

            var recordFetch = await repo.GetClientRecord();
            Assert.DoesNotContain(recordFetch.clientRecord.Clients, kvp => kvp.Key == clientUuid);

            Assert.False(perBucketCleaner.Running);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task DefaultBucket_MultipleClient(int clientCount)
        {
            var loggerFactory = new ClusterFixture.TestOutputLoggerFactory(_outputHelper);
            var clients = new List<PerBucketCleaner>(clientCount);
            var activeClients = new List<string>();
            try
            {
                for (int i = 0; i < clientCount; i++)
                {
                    var clientUuid = Guid.NewGuid().ToString();
                    var cleaner = new Cleaner(_fixture.Cluster, null, loggerFactory);
                    var collection = await _fixture.OpenDefaultCollection(_outputHelper);
                    var repo = new CleanerRepository(collection, null);
                    var perBucketCleaner = new PerBucketCleaner(clientUuid, cleaner, repo, TimeSpan.FromSeconds(0.1), loggerFactory, startDisabled: true);
                    clients.Add(perBucketCleaner);
                    var details = await perBucketCleaner.ProcessClient(cleanupAtrs: false);
                    activeClients.Add(clientUuid);
                    foreach (var activeClient in activeClients)
                    {
                        Assert.Contains(details.ActiveClientIds, cid => cid == activeClient);
                    }
                }
            }
            finally
            {
                foreach (var client in clients)
                {
                    await client.DisposeAsync();
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
