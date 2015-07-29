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
                var query = "SELECT * FROM `beer-sample` LIMIT 10";

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
                Assert.IsNull(result.Metrics);
            }
        }

        [Test]
        public async void Test_QueryAsync_With_QueryRequest_With_Metrics_true()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT $1")
                    .AddPositionalParameter(10)
                    .Metrics(true);

                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
                Assert.IsNotNull(result.Metrics);
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

        [Test]
        public void When_Prepare_A_Plan_Is_Returned()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var result = bucket.Prepare("SELECT * FROM `beer-sample` LIMIT 10");

                Assert.AreEqual(QueryStatus.Success, result.Status);
                Assert.AreEqual(1, result.Rows.Count);
            }
        }

        [Test]
        public void When_Possessing_A_Plan_It_Can_Be_Executed()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var planResult = bucket.Prepare("SELECT * FROM `beer-sample` LIMIT 10");
                var plan = planResult.Rows.First();

                Assert.AreEqual(QueryStatus.Success, planResult.Status);
                Assert.AreEqual(1, planResult.Rows.Count);

                var executionRequest = new QueryRequest(plan); //can also be constructed via Create factory method
                var executionResult = bucket.Query<dynamic>(executionRequest);

                Assert.AreEqual(QueryStatus.Success, executionResult.Status);
                Assert.AreEqual(10, executionResult.Rows.Count);
            }
        }

        [Test]
        public void When_Preparing_An_Invalid_Statement_An_Error_Is_Returned()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var result = bucket.Prepare("SELECT * FRO `beer-sample` LIMIT 10");
                Assert.IsFalse(result.Success);
                Assert.AreEqual(0, result.Rows.Count);
                Assert.AreEqual(QueryStatus.Fatal, result.Status);
                Assert.IsNotEmpty(result.Errors);
            }
        }

        [Test]
        public void Test_Invalid_Insert_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                     .Statement("INSERT INTO `beer-sample` VALUES ('foo1', {'bar' , 'baz'})")
                     .Signature(false);
                var result = bucket.Query<dynamic>(queryRequest);

                Assert.IsFalse(result.Success);
            }
        }

        [Test]
        public void Test_Insert_And_Update_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("INSERT INTO `beer-sample` VALUES ('testkey', {'foo' : 'bar'})")
                    .Signature(false);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                        .Statement("UPDATE `beer-sample` USE KEYS 'testkey' SET foo='baz'")
                        .Signature(false)
                        .ScanConsistency(ScanConsistency.RequestPlus);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest().Statement("DELETE FROM `beer-sample` USE KEYS 'testkey'")
                    .Signature(false);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
            }

        }

        [Test]
        public void Tests_Insert_Update_And_Delete_Positional_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("INSERT INTO `beer-sample` VALUES ('testkey', {'foo' : $1})")
                    .AddPositionalParameter("bar")
                    .Signature(false);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("UPDATE `beer-sample` USE KEYS 'testkey' SET foo=$1")
                    .AddPositionalParameter("baz")
                    .Signature(false)
                    .ScanConsistency(ScanConsistency.RequestPlus);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("DELETE FROM `beer-sample` USE KEYS $1;")
                    .AddPositionalParameter("testkey")
                    .Signature(false);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
            }

        }

        [Test]
        public void Tests_Insert_Update_And_Delete_Parameterized_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("INSERT INTO `beer-sample` VALUES ('testkey', {'foo' : $val})")
                    .AddNamedParameter("val", "bar")
                    .Signature(false);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("UPDATE `beer-sample` USE KEYS 'testkey' SET foo=$val")
                    .AddNamedParameter("val", "baz")
                    .Signature(false)
                    .ScanConsistency(ScanConsistency.RequestPlus);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("DELETE FROM `beer-sample` USE KEYS $key")
                    .AddNamedParameter("key", "testkey")
                    .Signature(false);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
            }

        }

    }
}
