using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.N1QL;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucket_N1QlQuery_Tests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(Utils.TestConfiguration.GetConfiguration("multiplexio"));
            _bucket = _cluster.OpenBucket();
        }

        [Test]
        public void Test_QueryNoDeadlock()
        {
            // Using an asynchronous HttpClient request within an MVC Web API action may cause
            // a deadlock when we wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucket.Query<dynamic>("SELECT * FROM `beer-sample` LIMIT 10");

                // If view queries are incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public void Test_QueryAsyncNoDeadlock()
        {
            // Using an asynchronous view query within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                _bucket.QueryAsync<dynamic>("SELECT * FROM `beer-sample` LIMIT 10").Wait();

                // If view queries are incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Test]
        public void Test_ReadYourOwnWrite()
        {
            Assert.IsTrue(_bucket.SupportsEnhancedDurability);

            var doc = new Document<DocumentContent>
            {
                Id = "Test_ReadYourOwnWrite",
                Content = new DocumentContent()
                {
                    Value = new Random().Next(0, 100000)
                }
            };

            var result = _bucket.Upsert(doc);
            Assert.True(result.Success);

            var state = MutationState.From(result.Document);

            var request = new QueryRequest("SELECT d.* FROM default as d WHERE `value` = $1 LIMIT 1")
                .AddPositionalParameter(doc.Content.Value)
                .ConsistentWith(state);

            var queryResult = _bucket.Query<DocumentContent>(request);

            Assert.True(queryResult.Success, queryResult.ToString());
            Assert.IsNotEmpty(queryResult.Rows);
            Assert.AreEqual(doc.Content.Value, queryResult.Rows.First().Value);
        }

        [Test]
        public void Test_ScanWait()
        {
            var doc = new Document<DocumentContent>
            {
                Id = "Test_ReadYourOwnWrite_WaitScan",
                Content = new DocumentContent
                {
                    Value = new Random().Next(0, 100000)
                }
            };

            var result = _bucket.Upsert(doc);
            Assert.True(result.Success);

            var state = MutationState.From(result.Document);

            var request = new QueryRequest("SELECT d.* FROM default as d WHERE `value` = $1 LIMIT 1")
                .AddPositionalParameter(doc.Content.Value)
                .ConsistentWith(state)
                .ScanWait(TimeSpan.FromSeconds(10));

            var queryResult = _bucket.Query<DocumentContent>(request);

            Assert.True(queryResult.Success, queryResult.ToString());
            Assert.IsNotEmpty(queryResult.Rows);
            Assert.AreEqual(doc.Content.Value, queryResult.Rows.First().Value);
        }

        public class DocumentContent
        {
            public int Value { get; set; }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.CloseBucket(_bucket);
            _cluster.Dispose();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

#endregion
