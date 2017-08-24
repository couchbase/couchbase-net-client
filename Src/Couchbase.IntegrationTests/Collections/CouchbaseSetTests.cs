using System;
using System.Linq;
using Couchbase.Collections;
using Couchbase.Core;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Collections
{
    [TestFixture]
    public class CouchbaseSetTests
    {
        public class Poco
        {
            public string Key { get; set; }

            public string Name { get; set; }

            protected bool Equals(Poco other)
            {
                return string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Poco) obj);
            }

            public override int GetHashCode()
            {
                return (Key != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Key) : 0);
            }
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
            var collection = new CouchbaseSet<Poco>(_bucket, "CouchbaseSetTests_Test_Add");
            collection.Clear();

            collection.Add(new Poco {Key = "poco1", Name = "Poco-pica"});
            collection.Add(new Poco {Key = "poco2", Name = "Poco-pica2"});
        }

        [Test]
        public void Test_Clear()
        {
            var collection = new CouchbaseSet<Poco>(_bucket, "CouchbaseSetTests_Test_Clear");
            collection.Add(new Poco { Key = "poco1", Name = "Poco-pica" });
            collection.Clear();

            Assert.AreEqual(0, collection.Count);
        }

        [Test]
        public void Test_Remove()
        {
            var collection = new CouchbaseSet<Poco>(_bucket, "CouchbaseSetTests_Test_Clear");
            collection.Add(new Poco { Key = "poco1", Name = "Poco-pica" });
            collection.Remove(new Poco {Key = "poco1", Name = "Poco-pica"});

            var actual = collection.Contains(new Poco {Key = "poco1", Name = "Poco-pica"});

            Assert.AreEqual(false, actual);
        }

        [Test]
        public void Test_Add_WhenExists_ThrowInvalidOperationException()
        {
            var collection = new CouchbaseSet<Poco>(_bucket, "Test_Add_WhenExists_ThrowInvalidOPerationException");
            collection.Clear();

            collection.Add(new Poco {Key = "poco1", Name = "Poco-pica"});
            Assert.Throws<InvalidOperationException>(() => collection.Add(new Poco { Key = "poco1", Name = "Poco-pica" }));
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
