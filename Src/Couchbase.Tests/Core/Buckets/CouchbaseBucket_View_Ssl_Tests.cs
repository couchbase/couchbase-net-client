using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Buckets
{
    [TestFixture]
    public class CouchbaseBucketViewSslTests
    {
        private ICluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var bootstrapUrl = ConfigurationManager.AppSettings["bootstrapUrl"];
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>{ new Uri(bootstrapUrl)},
                UseSsl = true
            };
            _cluster = new Cluster(config);
        }

        [Test]
        public void When_UseSsl_True_CreateQuery_Returns_Https_Url()
        {
            var expected = new Uri("https://localhost:18092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample");

            var query = bucket.CreateQuery("beer", "brewery_beers").
                Development(true).
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected.Port, query.Port);
            Assert.AreEqual(expected.Scheme, query.Scheme);
            Assert.AreEqual(expected.PathAndQuery, query.PathAndQuery);
        }

        [Test]
        public void When_UseSsl_True_CreateQuery2_Returns_Https_Url()
        {
            var expected = new Uri("https://localhost:18092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample");
            var query = bucket.CreateQuery("beer", "brewery_beers").
                Development(true).
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected.Port, query.Port);
            Assert.AreEqual(expected.Scheme, query.Scheme);
            Assert.AreEqual(expected.PathAndQuery, query.PathAndQuery);
        }

        [Test]
        public void When_UseSsl_True_CreateQuery3_Returns_Https_Url()
        {
            var expected = new Uri("https://localhost:18092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample");
            var query = bucket.CreateQuery("beer", "brewery_beers").
                Development(true).
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected.Port, query.Port);
            Assert.AreEqual(expected.Scheme, query.Scheme);
            Assert.AreEqual(expected.PathAndQuery, query.PathAndQuery);
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