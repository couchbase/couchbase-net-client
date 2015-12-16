using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.N1QL;
using NUnit.Framework;
using Couchbase.Utils;

namespace Couchbase.Tests
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
        public void When_Bucket_Requires_Authentication_And_Credentials_Provided_Query_Succeeds()
        {
            using (var bucket = _cluster.OpenBucket("authenticated", "secret"))
            {
                //NOTE: authenticated is password protected
                const string query = "SELECT * FROM `authenticated` LIMIT 10";

                var queryRequest = new QueryRequest(query);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void When_Bucket_Requires_Authentication_And_Credentials_NotProvided_Query_Fails()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                //NOTE: authenticated is password protected
                const string query = "SELECT * FROM `authenticated` LIMIT 10";

                var queryRequest = new QueryRequest(query);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsFalse(result.Success);
            }
        }

        [Test]
        public async void When_Bucket_Requires_Authentication_And_Credentials_Provided_QueryAsync_Succeeds()
        {
            using (var bucket = _cluster.OpenBucket("authenticated", "secret"))
            {
                //NOTE: authenticated is password protected
                const string query = "SELECT * FROM `authenticated` LIMIT 10";

                var queryRequest = new QueryRequest(query);
                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public async void When_Bucket_Requires_Authentication_And_Credentials_NotProvided_QueryAsync_Fails()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                //NOTE: authenticated is password protected
                const string query = "SELECT * FROM `authenticated` LIMIT 10";

                var queryRequest = new QueryRequest(query);
                var result = await bucket.QueryAsync<dynamic>(queryRequest);
                Assert.IsFalse(result.Success);
            }
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
        public void When_MaxServerParallelism_Is_Set_Request_Succeeds()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest("SELECT * FROM default LIMIT 10;");
                queryRequest.MaxServerParallelism(4);

                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
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
        public void When_Bucket_Has_No_PrimaryIndex_Status_Is_Fatal()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `default` LIMIT 10");

                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsFalse(result.Success);
                Assert.AreEqual(QueryStatus.Fatal, result.Status);
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
        public void Test_Invalid_Insert_Query()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                var queryRequest = new QueryRequest()
                    .Statement("INSERT INTO `default` VALUES ('foo1', {'bar' , 'baz'})")
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
                bucket.Remove("testdmlkey");
                var queryRequest = new QueryRequest()
                    .Statement("INSERT INTO `default` VALUES ('testdmlkey', {'foo' : 'bar'})")
                    .Signature(false);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success, result.GetErrorsAsString());

                queryRequest = new QueryRequest()
                    .Statement("UPDATE `default` USE KEYS 'testdmlkey' SET foo='baz'")
                        .Signature(false)
                        .ScanConsistency(ScanConsistency.RequestPlus);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest().Statement("DELETE FROM `default` USE KEYS 'testdmlkey'")
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
                bucket.Remove("testdmlkey");
                var queryRequest = new QueryRequest()
                    .Statement("INSERT INTO `default` VALUES ('testdmlkey', {'foo' : $1})")
                    .AddPositionalParameter("bar")
                    .Signature(false);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("UPDATE `default` USE KEYS 'testdmlkey' SET foo=$1")
                    .AddPositionalParameter("baz")
                    .Signature(false)
                    .ScanConsistency(ScanConsistency.RequestPlus);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("DELETE FROM `default` USE KEYS $1;")
                    .AddPositionalParameter("testdmlkey")
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
                bucket.Remove("testdmlkey");
                var queryRequest = new QueryRequest()
                    .Statement("INSERT INTO `default` VALUES ('testdmlkey', {'foo' : $val})")
                    .AddNamedParameter("val", "bar")
                    .Signature(false);
                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("UPDATE `default` USE KEYS 'testdmlkey' SET foo=$val")
                    .AddNamedParameter("val", "baz")
                    .Signature(false)
                    .ScanConsistency(ScanConsistency.RequestPlus);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);

                queryRequest = new QueryRequest()
                    .Statement("DELETE FROM `default` USE KEYS $key")
                    .AddNamedParameter("key", "testdmlkey")
                    .Signature(false);
                result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void When_Adhoc_Query_Is_Used_Plan_Is_Prepared_And_Cached()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                ((IQueryCacheInvalidator)bucket).InvalidateQueryCache();
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 1")
                    .AdHoc(false);

                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success, result.GetErrorsAsString());
                var count = ((IQueryCacheInvalidator) bucket).InvalidateQueryCache();
                Assert.IsFalse(queryRequest.IsAdHoc);
                Assert.IsTrue(queryRequest.IsPrepared);
                Assert.Greater(count, 0);
            }
        }

        [Test]
        public void When_Adhoc_Is_True_Query_Is_Not_Prepared_And_Cached()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                ((IQueryCacheInvalidator)bucket).InvalidateQueryCache();
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 1")
                    .AdHoc(true);

                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success, result.GetErrorsAsString());
                var count = ((IQueryCacheInvalidator)bucket).InvalidateQueryCache();
                Assert.IsTrue(queryRequest.IsAdHoc);
                Assert.IsFalse(queryRequest.IsPrepared);
                Assert.AreEqual(count, 0);
            }
        }

        [Test]
        public void When_Adhoc_Is_False_QueryPlan_Is_Reused()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                ((IQueryCacheInvalidator)bucket).InvalidateQueryCache();
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 1")
                    .AdHoc(false);

                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success, result.GetErrorsAsString());

                var queryRequest1 = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 1")
                    .AdHoc(false);

                result = bucket.Query<dynamic>(queryRequest1);
                Assert.IsTrue(result.Success, result.GetErrorsAsString());

                var count = ((IQueryCacheInvalidator) bucket).InvalidateQueryCache();
                Assert.IsTrue(queryRequest.IsPrepared);
                Assert.IsFalse(queryRequest.IsAdHoc);
                Assert.AreEqual(count, 1);
            }
        }

        [Test]
        public void When_Adhoc_Is_False_QueryPlan_From_Different_Queries_Are_Cached()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                ((IQueryCacheInvalidator)bucket).InvalidateQueryCache();
                var queryRequest = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 1")
                    .AdHoc(false);

                var result = bucket.Query<dynamic>(queryRequest);
                Assert.IsTrue(result.Success, result.GetErrorsAsString());

                var queryRequest1 = new QueryRequest()
                    .Statement("SELECT * FROM `beer-sample` LIMIT 2")
                    .AdHoc(false);

                result = bucket.Query<dynamic>(queryRequest1);
                Assert.IsTrue(result.Success, result.GetErrorsAsString());

                var count = ((IQueryCacheInvalidator) bucket).InvalidateQueryCache();
                Assert.IsTrue(queryRequest.IsPrepared);
                Assert.IsFalse(queryRequest.IsAdHoc);
                Assert.AreEqual(count, 2);
            }
        }

        [Test]
        public void When_Amp_And_Dollar_Are_Used_In_Insert_Values_Encoding_Is_Correct() {
            using (var bucket = _cluster.OpenBucket())
            {
                var key = "Y-11db7305-6adb-4237-b6d7-fa4a3497d2e9";
                bucket.Remove(key);

                var query = "INSERT INTO `default` (KEY, VALUE) VALUES (\"" + key + "\", " +
                    "{ \"0\": \"Y\", \"1\": \"&AB\", \"v\": \"$C(38)_AB\" })";

                var request = QueryRequest.Create(query);
                request.ScanConsistency(ScanConsistency.RequestPlus);
                var queryResult = bucket.Query<dynamic>(request);

                Assert.IsTrue(queryResult.Success, queryResult.GetErrorsAsString());
                Assert.AreEqual(1, queryResult.Metrics.MutationCount);
            }
        }
    }
}
