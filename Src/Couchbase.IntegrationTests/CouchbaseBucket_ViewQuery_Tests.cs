using System.Linq;
using System.Threading;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Views;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucketViewQueryTests
    {
        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _cluster = new Cluster(TestConfiguration.GetConfiguration("basic"));
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket("beer-sample");
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
                var query = _bucket.CreateQuery("beer", "brewery_beers")
                    .Limit(1);

                _bucket.Query<dynamic>(query);

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
            // NCBC-1074 https://issues.couchbase.com/browse/NCBC-1074
            // Using an asynchronous view query within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                var query = _bucket.CreateQuery("beer", "brewery_beers")
                    .Limit(1);

                _bucket.QueryAsync<object>(query).Wait();

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
        public void Use_Streaming()
        {
            var query = _bucket.CreateQuery("beer", "brewery_beers")
                .Limit(10)
                .UseStreaming(true);

            var count = 0;
            var result = _bucket.Query<dynamic>(query);
            foreach (var row in result.Rows)
            {
                count++;
                Assert.IsNotNull(row);
                Assert.IsNotNull(row.Id);
            }

            Assert.AreEqual(10, count);
            Assert.IsAssignableFrom<StreamingViewResult<dynamic>>(result);
        }

        [Test]
        public void Can_Submit_Lots_of_Keys()
        {
            var query = _bucket.CreateQuery("beer", "brewery_beers")
                .Keys(Enumerable.Range(1, 1000).Select(i => $"key-{i}"));

            var result = _bucket.Query<dynamic>(query);
            Assert.IsTrue(result.Success);
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
