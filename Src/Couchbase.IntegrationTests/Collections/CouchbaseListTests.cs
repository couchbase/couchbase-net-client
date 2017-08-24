using System.Linq;
using Couchbase.Collections;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Collections
{
    [TestFixture]
    public class CouchbaseListTests
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
            _cluster.SetupEnhancedAuth();
            _bucket = _cluster.OpenBucket();
        }

        [Test]
        public void Test_Add()
        {
            var collection = new CouchbaseList<Poco>(_bucket, "BucketListTests_Test_Add");

            collection.Add(new Poco { Key = "poco1", Name = "Poco-pica" });

            var item = collection[0];
            Assert.AreEqual("poco1", item.Key);
        }

        [Test]
        public void Test_Enumeration()
        {
            var collection = new CouchbaseList<Poco>(_bucket, "BucketListTests_Test_Enumeration");

            var numItems = 5;

            for (var i = 0; i < numItems; i++)
            {
                collection.Add(new Poco {Key = "poco"+i, Name = "Poco-pica"+i});
            }

            foreach (var poco in collection)
            {
                Assert.IsNotNull(poco);
            }
        }

        [Test]
        public void Test_Clear()
        {
            var collection = new CouchbaseList<Poco>(_bucket, "BucketListTests_Test_Clear");

            collection.Add(new Poco { Key = "poco2", Name = "Poco-pica" });
            collection.Clear();

            var count = collection.Count;
            Assert.AreEqual(0, count);
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
