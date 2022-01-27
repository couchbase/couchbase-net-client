#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Transactions.Config;
using Couchbase.Transactions.Tests.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace Couchbase.Transactions.Tests.IntegrationTests
{
    internal static class TestUtil
    {
        public static async Task<DurabilityLevel> InsertAndVerifyDurability(ICouchbaseCollection defaultCollection, string docId,
            object sampleDoc)
        {
            var durability = DurabilityLevel.Majority;

            try
            {
                _ = await defaultCollection.InsertAsync(docId, sampleDoc, opts => opts.Durability(durability).Expiry(TimeSpan.FromMinutes(10)));
            }
            catch (DurabilityImpossibleException ex)
            {
                throw new InvalidOperationException("Bucket must support Durability.Majority, at least.", ex);
            }

            return durability;
        }

        public record SampleDoc(string id, string? type, string? foo, long? revision);

        public static async Task<(ICouchbaseCollection collection, string docId, SampleDoc sampleDoc)> PrepSampleDoc(ClusterFixture fixture, ITestOutputHelper outputHelper, [CallerMemberName]string testName = nameof(PrepSampleDoc))
        {
            var defaultCollection = await fixture.OpenDefaultCollection(outputHelper);
            var docId = Guid.NewGuid().ToString();
            var sampleDoc = new SampleDoc(docId, testName, "bar", 100);
            return (defaultCollection, docId, sampleDoc);
        }

        public static Transactions CreateTransaction(ICluster cluster, DurabilityLevel durability, ITestOutputHelper outputHelper)
        {
            var configBuilder = TransactionConfigBuilder.Create()
                .DurabilityLevel(durability)
                .LoggerFactory(new ClusterFixture.TestOutputLoggerFactory(outputHelper));
            if (Debugger.IsAttached)
            {
                // don't expire when watching the debugger.
                configBuilder.ExpirationTime(TimeSpan.FromMinutes(1000));
            }

            var txn = Transactions.Create(cluster, configBuilder.Build());
            return txn;
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
