using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Collections;
using Couchbase.Core;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Collections
{
    [TestFixture]
    public class CouchbaseQueueTests
    {
        public class Poco
        {
            public string Key { get; set; }

            public string Name { get; set; }
        }

        private ICluster _cluster;
        private IBucket _bucket;

        [OneTimeSetUp]
        public void Setup()
        {
            var config = Utils.TestConfiguration.GetCurrentConfiguration();
            config.BucketConfigs.First().Value.UseEnhancedDurability = false;
            _cluster = new Cluster(config);
            _bucket = _cluster.OpenBucket();
        }

        [Test]
        public void Test_Dequeue()
        {
            var key = "CouchbaseQueueTests.Test_Dequeue";
            _bucket.Remove(key);

            var queue = new CouchbaseQueue<Poco>(_bucket, key);
            queue.Enqueue(new Poco { Name = "pcoco1" });
            queue.Enqueue(new Poco { Name = "pcoco2" });
            queue.Enqueue(new Poco { Name = "pcoco3" });

            var item = queue.Dequeue();
            Assert.AreEqual("pcoco1", item.Name);

            var items = _bucket.Get<List<Poco>>(key).Value;
            Assert.AreEqual(2, items.Count);
        }

        [Test]
        public void Test_Enqueue()
        {
            var key = "CouchbaseQueueTests.Test_Enqueue";
            _bucket.Remove(key);

            var queue = new CouchbaseQueue<Poco>(_bucket, "CouchbaseQueueTests.Test_Enqueue");
            queue.Enqueue(new Poco {Name = "pcoco"});

            var items = _bucket.Get<List<Poco>>(key).Value;
            Assert.AreEqual(1, items.Count);
        }

        [Test]
        public void Test_Peek()
        {
            var key = "CouchbaseQueueTests.Test_Peek";
            _bucket.Remove(key);

            var queue = new CouchbaseQueue<Poco>(_bucket, key);
            queue.Enqueue(new Poco { Name = "pcoco1" });
            queue.Enqueue(new Poco { Name = "pcoco2" });
            queue.Enqueue(new Poco { Name = "pcoco3" });

            var item = queue.Peek();
            Assert.AreEqual("pcoco1", item.Name);

            var items = _bucket.Get<List<Poco>>(key).Value;
            Assert.AreEqual(3, items.Count);
        }

        [Test]
        public void Test_Peek_Throws_InvalidOperationException_When_Empty()
        {
            var key = "Test_Peek_Throws_InvalidOperationException_When_Empty";
            _bucket.Remove(key);

            var queue = new CouchbaseQueue<Poco>(_bucket, key);

            Assert.Throws<InvalidOperationException>(() => queue.Peek());
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
