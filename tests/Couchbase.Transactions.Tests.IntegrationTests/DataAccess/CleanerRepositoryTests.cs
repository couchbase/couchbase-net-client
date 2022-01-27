using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Transactions.Cleanup.LostTransactions;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.DataAccess;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using TestOutputLoggerFactory = Couchbase.Transactions.Tests.IntegrationTests.Fixtures.ClusterFixture.TestOutputLoggerFactory;

namespace Couchbase.Transactions.Tests.IntegrationTests.DataAccess
{
    public class CleanerRepositoryTests : IClassFixture<ClusterFixture>
    {
        private ClusterFixture _fixture;
        private ITestOutputHelper _outputHelper;

        public CleanerRepositoryTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            ClusterFixture.LogLevel = LogLevel.Debug;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task CreateRetrieveDeleteClientRecord()
        {
            string clientUuid = Guid.NewGuid().ToString();
            _outputHelper.WriteLine($"clientUuid = {clientUuid}");
            TimeSpan testCleanupWindow = TimeSpan.FromSeconds(2.51);
            var cluster = _fixture.GetCluster();
            var loggerFactory = new ClusterFixture.TestOutputLoggerFactory(_outputHelper);
            var collection = await _fixture.OpenDefaultCollection(_outputHelper);
            var repo = new CleanerRepository(collection, null);
            try
            {
                await repo.CreatePlaceholderClientRecord();
                _outputHelper.WriteLine("ClientRecord created fresh.");
            }
            catch (DocumentExistsException)
            {
                _outputHelper.WriteLine("ClientRecord already exists.");
            }
            catch (CasMismatchException)
            {
                _outputHelper.WriteLine("ClientRecord already exists.");
            }

            {
                (var clientRecordsIndex, var parsedHlc, _) = await repo.GetClientRecord();
                Assert.NotNull(clientRecordsIndex);
                Assert.NotNull(parsedHlc);
                _outputHelper.WriteLine($"Initial ClientRecord:\n{JObject.FromObject(clientRecordsIndex)}");
                var clientRecordDetails = new ClientRecordDetails(clientRecordsIndex, parsedHlc, clientUuid, testCleanupWindow);
                _outputHelper.WriteLine($"Initial ClientRecordDetails:\n{JObject.FromObject(clientRecordDetails)}");
                await repo.UpdateClientRecord(clientUuid, testCleanupWindow, ActiveTransactionRecords.AtrIds.NumAtrs, clientRecordDetails.ExpiredClientIds);
            }

            {
                (var clientRecordsIndex, var parsedHlc, _) = await repo.GetClientRecord();
                Assert.NotNull(clientRecordsIndex);
                Assert.NotNull(parsedHlc);
                _outputHelper.WriteLine($"Updated ClientRecord:\n{JObject.FromObject(clientRecordsIndex)}");
                var clientRecordDetails = new ClientRecordDetails(clientRecordsIndex, parsedHlc, clientUuid, testCleanupWindow);
                _outputHelper.WriteLine($"Updated ClientRecordDetails:\n{JObject.FromObject(clientRecordDetails)}");
                Assert.DoesNotContain(clientRecordDetails.ExpiredClientIds, s => s == clientUuid);
                Assert.Contains(clientRecordDetails.ActiveClientIds, s => s == clientUuid);
                foreach (var expiredId in clientRecordDetails.ExpiredClientIds)
                {
                    Assert.DoesNotContain(clientRecordDetails.ActiveClientIds, s => s == expiredId);
                }
            }

            await repo.RemoveClient(clientUuid, KeyValue.DurabilityLevel.Majority);
            {
                (var clientRecordsIndex, var parsedHlc, _) = await repo.GetClientRecord();
                Assert.NotNull(clientRecordsIndex);
                Assert.NotNull(parsedHlc);
                _outputHelper.WriteLine($"After Remove ClientRecord:\n{JObject.FromObject(clientRecordsIndex)}");
                var clientRecordDetails = new ClientRecordDetails(clientRecordsIndex, parsedHlc, clientUuid, testCleanupWindow);
                _outputHelper.WriteLine($"After Remove ClientRecordDetails:\n{JObject.FromObject(clientRecordDetails)}");
                Assert.DoesNotContain(clientRecordDetails.ExpiredClientIds, s => s == clientUuid);
                Assert.DoesNotContain(clientRecordsIndex.Clients, s => s.Key == clientUuid);
                foreach (var expiredId in clientRecordDetails.ExpiredClientIds)
                {
                    Assert.DoesNotContain(clientRecordDetails.ActiveClientIds, s => s == expiredId);
                }
            }
        }

        [Fact]
        public async Task TwoClientsDifferentAtrs()
        {
            string clientA = Guid.NewGuid().ToString();
            string clientB = Guid.NewGuid().ToString();
            _outputHelper.WriteLine($"clientA = {clientA}");
            _outputHelper.WriteLine($"clientB = {clientA}");
            TimeSpan testCleanupWindow = TimeSpan.FromSeconds(2.51);
            var cluster = _fixture.GetCluster();
            var loggerFactory = new ClusterFixture.TestOutputLoggerFactory(_outputHelper);
            var collection = await _fixture.OpenDefaultCollection(_outputHelper);
            var repo = new CleanerRepository(collection, null);
            try
            {
                await repo.CreatePlaceholderClientRecord();
                _outputHelper.WriteLine("ClientRecord created fresh.");
            }
            catch (DocumentExistsException)
            {
                _outputHelper.WriteLine("ClientRecord already exists.");
            }
            catch (CasMismatchException)
            {
                _outputHelper.WriteLine("ClientRecord already exists.");
            }

            {
                (var clientRecordsIndex, var parsedHlc, _) = await repo.GetClientRecord();
                Assert.NotNull(clientRecordsIndex);
                Assert.NotNull(parsedHlc);
                _outputHelper.WriteLine($"Initial ClientRecord before A created:\n{JObject.FromObject(clientRecordsIndex)}");
                var clientRecordDetails = new ClientRecordDetails(clientRecordsIndex, parsedHlc, clientA, testCleanupWindow);
                _outputHelper.WriteLine($"Initial ClientRecordDetails clientA:\n{JObject.FromObject(clientRecordDetails)}");
                Assert.NotEqual(-1, clientRecordDetails.IndexOfThisClient);
                await repo.UpdateClientRecord(clientA, testCleanupWindow, ActiveTransactionRecords.AtrIds.NumAtrs, clientRecordDetails.ExpiredClientIds);
            }

            {
                (var clientRecordsIndex, var parsedHlc, _) = await repo.GetClientRecord();
                Assert.NotNull(clientRecordsIndex);
                Assert.NotNull(parsedHlc);
                _outputHelper.WriteLine($"Initial ClientRecord before B created.:\n{JObject.FromObject(clientRecordsIndex)}");
                var clientRecordDetails = new ClientRecordDetails(clientRecordsIndex, parsedHlc, clientB, testCleanupWindow);
                _outputHelper.WriteLine($"Initial ClientRecordDetails clientB:\n{JObject.FromObject(clientRecordDetails)}");
                Assert.NotEqual(-1, clientRecordDetails.IndexOfThisClient);
                await repo.UpdateClientRecord(clientB, testCleanupWindow, ActiveTransactionRecords.AtrIds.NumAtrs, clientRecordDetails.ExpiredClientIds);
            }

            {
                (var clientRecordsIndex, var parsedHlc, _) = await repo.GetClientRecord();
                Assert.NotNull(clientRecordsIndex);
                Assert.NotNull(parsedHlc);
                _outputHelper.WriteLine($"Updated ClientRecord after both created:\n{JObject.FromObject(clientRecordsIndex)}");
                var clientRecordDetailsA = new ClientRecordDetails(clientRecordsIndex, parsedHlc, clientA, testCleanupWindow);
                _outputHelper.WriteLine($"Updated ClientRecordDetails clientA:\n{JObject.FromObject(clientRecordDetailsA)}");
                var clientRecordDetailsB = new ClientRecordDetails(clientRecordsIndex, parsedHlc, clientB, testCleanupWindow);
                _outputHelper.WriteLine($"Updated ClientRecordDetails clientB:\n{JObject.FromObject(clientRecordDetailsB)}");
                Assert.DoesNotContain(clientRecordDetailsA.AtrsHandledByThisClient, s => clientRecordDetailsB.AtrsHandledByThisClient.Contains(s));
            }

            await repo.RemoveClient(clientA);
            await repo.RemoveClient(clientB);
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