using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Tests.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseBucketViewTests
    {
        private ICluster _cluster;
        private string _serverIp;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new Cluster(ClientConfigUtil.GetConfiguration());
            _serverIp = ConfigurationManager.AppSettings["serverIp"];
        }

        [Test]
        public void Test_CreateQuery_Overload2()
        {
            var expected = new Uri(string.Format("http://{0}:8092/beer-sample/_design/dev_beer/_view/brewery_beers?", _serverIp));
            var bucket = _cluster.OpenBucket("beer-sample");
            var query = bucket.CreateQuery("beer", "brewery_beers", true).
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected.Scheme, query.Scheme);
            Assert.AreEqual(expected.PathAndQuery, query.PathAndQuery);
        }

        [Test]
        public void Test_CreateQuery_Overload3()
        {
            var expected = new Uri(string.Format("http://{0}:8092/beer-sample/_design/dev_beer/_view/brewery_beers?", _serverIp));
            var bucket = _cluster.OpenBucket("beer-sample");
            var query = bucket.CreateQuery("beer", "brewery_beers", true).
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected.Scheme, query.Scheme);
            Assert.AreEqual(expected.PathAndQuery, query.PathAndQuery);
        }

        [Test]
        public void When_BucketName_Is_Not_Set_The_Buckets_Name_Is_Used()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var query = bucket.CreateQuery("beer", "brewery_beers", true);
                Assert.AreEqual(bucket.Name, query.BucketName);
            }
        }

        [Test]
        public void When_BucketName_Is_Not_Set_The_Buckets_Name_Is_Used2()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var query = bucket.CreateQuery("beer", "brewery_beers");
                Assert.AreEqual(bucket.Name, query.BucketName);
            }
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            if (_cluster != null)
            {
                _cluster.Dispose();
                _cluster = null;
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
