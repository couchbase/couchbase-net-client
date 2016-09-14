
using System.Linq;
using Couchbase.Collections;
using Couchbase.Core;
using NUnit.Framework;
using NUnit.Framework.Internal;

namespace Couchbase.IntegrationTests.Collections
{
    [TestFixture]
    public class CouchbaseDictionaryTests
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
        public void Test_Add()
        {
            const string key = "CouchbaseDictionaryTests.Test_Add";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey1", new Poco {Name = "poco1"});
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            Assert.AreEqual(2, dictionary.Count);
        }

        [Test]
        public void Test_Remove()
        {
            const string key = "CouchbaseDictionaryTests.Test_Clear";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey1", new Poco { Name = "poco1" });
            dictionary.Add("somekey2", new Poco { Name = "poco2" });
            var result = dictionary.Remove("somekey2");

            Assert.IsTrue(result);
            Assert.AreEqual(1, dictionary.Count);
        }

        [Test]
        public void Test_Clear()
        {
            const string key = "CouchbaseDictionaryTests.Test_Clear";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey1", new Poco { Name = "poco1" });
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            dictionary.Clear();
            var count = dictionary.Count;

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Test_Indexer_Get()
        {
            const string key = "CouchbaseDictionaryTests.Test_Add";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey1", new Poco { Name = "poco1" });
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            var item = dictionary["somekey1"];
            Assert.AreEqual("poco1", item.Name);
        }

        [Test]
        public void Test_Indexer_Set()
        {
            const string key = "CouchbaseDictionaryTests.Test_Add";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey1", new Poco { Name = "poco1" });
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            var item = dictionary["somekey1"] = new Poco {Name = "poco3"};
            Assert.AreEqual("poco3", item.Name);
        }

        [Test]
        public void Test_TryGetValue()
        {
            const string key = "CouchbaseDictionaryTests.Test_TryGetValue";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            Poco item;
            var result = dictionary.TryGetValue("somekey2", out item);

            Assert.IsTrue(result);
            Assert.AreEqual("poco2", item.Name);
        }

        [Test]
        public void Test_TryGetValue_DoesNotExist()
        {
            const string key = "CouchbaseDictionaryTests.Test_TryGetValue_DoesNotExist";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            Poco item;
            var result = dictionary.TryGetValue("somekey3", out item);

            Assert.IsFalse(result);
            Assert.IsNull(item);
        }

        [Test]
        public void Test_ContainsKey_KeyIsFound()
        {
            const string key = "CouchbaseDictionaryTests.Test_ContainsKey_KeyIsFound";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey1", new Poco { Name = "poco1" });
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            var result = dictionary.ContainsKey("somekey1");
            Assert.IsTrue(result);
        }

        [Test]
        public void Test_ContainsKey_KeyIsNotFound()
        {
            const string key = "CouchbaseDictionaryTests.Test_ContainsKey_KeyIsNotFound";
            _bucket.Remove(key);

            var dictionary = new CouchbaseDictionary<string, Poco>(_bucket, key);
            dictionary.Add("somekey1", new Poco { Name = "poco1" });
            dictionary.Add("somekey2", new Poco { Name = "poco2" });

            var result = dictionary.ContainsKey("somekey");
            Assert.IsFalse(result);
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
