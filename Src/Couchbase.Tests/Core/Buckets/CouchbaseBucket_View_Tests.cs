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
        private ICluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Cluster.Initialize();
            _cluster = Cluster.Get();
        }

        [Test]
        public void Test_CreateView()
        {
            var expected = new Uri("http://localhost:8092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample") as ICouchbaseBucket;

            var query = bucket.CreateQuery(true).
                DesignDoc("beer").
                View("brewery_beers").
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected, query);
        }

        [Test]
        public void Test_CreateView_Overload2()
        {
            var expected = new Uri("http://localhost:8092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample") as ICouchbaseBucket;
            var query = bucket.CreateQuery("beer", true).
                View("brewery_beers").
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected, query);
        }

        [Test]
        public void Test_CreateView_Overload3()
        {
            var expected = new Uri("http://localhost:8092/beer-sample/_design/dev_beer/_view/brewery_beers?");
            var bucket = _cluster.OpenBucket("beer-sample") as ICouchbaseBucket;
            var query = bucket.CreateQuery("beer", "brewery_beers", true).
                RawUri();

            _cluster.CloseBucket(bucket);
            Assert.AreEqual(expected, query);
        }
    }
}
