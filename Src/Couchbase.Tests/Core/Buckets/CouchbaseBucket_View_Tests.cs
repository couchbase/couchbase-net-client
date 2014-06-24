using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class CouchbaseBucketViewTests
    {
        private ICouchbaseCluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new CouchbaseCluster();
        }

        [Test]
        public void Test_CreateQuery()
        {
            var expected = new Uri("http://localhost:8092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample");

            var query = bucket.CreateQuery(true).
                DesignDoc("beer").
                View("brewery_beers").
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected, query);
        }

        [Test]
        public void Test_CreateQuery_Overload2()
        {
            var expected = new Uri("http://localhost:8092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample");
            var query = bucket.CreateQuery(true, "beer").
                View("brewery_beers").
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected, query);
        }

        [Test]
        public void Test_CreateQuery_Overload3()
        {
            var expected = new Uri("http://localhost:8092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample");
            var query = bucket.CreateQuery(true, "beer", "brewery_beers").
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected, query);
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