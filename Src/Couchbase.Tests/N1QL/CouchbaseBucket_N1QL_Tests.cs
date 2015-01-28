using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.N1QL;
using NUnit.Framework;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class CouchbaseBucketN1QlTests
    {
        private Cluster _cluster;

        [SetUp]
        public void SetUp()
        {
            _cluster = new Cluster(new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            });
        }

        [TearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }

        [Test]
        public async void Test_QueryAsync()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string query = "SELECT * FROM `beer-sample` LIMIT 10";

                var result = await bucket.QueryAsync<dynamic>(query);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
            }
        }

        [Test]
        public void Test_N1QL_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                const string query = "SELECT * FROM `beer-sample` LIMIT 10";

                var result = bucket.Query<dynamic>(query);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
            }
        }

        [Test]
        public void Test_Query_With_QueryRequest()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 10");

                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
            }
        }

        [Test]
        public async void Test_QueryAsync_With_QueryRequest()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 10");

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
            }
        }

        [Test]
        public async void Test_QueryAsync_With_QueryRequest_With_PositionalParameters()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT $1")
                    .AddPositionalParameter(10);

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
            }
        }

        [Test]
        public async void Test_QueryAsync_With_QueryRequest_With_NamedParameters()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT $limit")
                    .AddNamedParameter("$limit", 10);

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
            }
        }

        [Test]
        public async void Test_QueryAsync_With_QueryRequest_With_Timeout()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT $1")
                    .AddPositionalParameter(10)
                    .Timeout(new TimeSpan(0, 0, 0, 0, 5));

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(QueryStatus.Timeout, result.Status);
                Assert.AreEqual(0, result.Rows.Count);
            }
        }

        [Test]
        public async void Test_QueryAsync_With_QueryRequest_With_Metrics_false()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT $1")
                    .AddPositionalParameter(10)
                    .Metrics(false);

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
                Assert.AreEqual(10, result.Metrics.ResultCount);//this shoulf fail when metrics=false works!
            }
        }

        [Test]
        public async void When_Signature_Is_False_Signature_Is_Not_Returned()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT $1")
                    .AddPositionalParameter(10)
                    .Signature(false);

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
                Assert.AreEqual(10, result.Rows.Count);
                Assert.IsNull(result.Signature);
            }
        }

        [Test]
        public async void When_Statement_Is_InValid_Errors_Are_Returned()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FRO `beer-sample` LIMIT $1")
                    .AddPositionalParameter(10)
                    .Signature(false);

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(0, result.Rows.Count);
                Assert.AreEqual(QueryStatus.Fatal, result.Status);
                Assert.IsNotEmpty(result.Errors);
            }
        }
    }
}
