using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.N1QL;
using Couchbase.Tests.Documents;
using Couchbase.Views;
using Newtonsoft.Json.Bson;
using NUnit.Framework;
using Couchbase.Utils;
using Couchbase.IO.Operations;

namespace Couchbase.Tests.N1QL
{
    [TestFixture]
    public class QueryClientTests
    {
        private readonly string _server = ConfigurationManager.AppSettings["serverIp"];

        [Test]
        public void Test_Create_Index()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));

            var indexes = client.Query<dynamic>(new QueryRequest("SELECT name FROM system:keyspaces").BaseUri(uri));
            foreach (var row in indexes.Rows)
            {
                if (row.GetValue("name").Value == "beer-sample")
                {
                    client.Query<dynamic>(new QueryRequest("DROP PRIMARY INDEX ON `beer-sample`").BaseUri(uri));
                }
            }

            var query = new QueryRequest("CREATE PRIMARY INDEX ON `beer-sample`")
                .BaseUri(uri)
                .Timeout(new TimeSpan(0, 0, 0, 60));

            var result = client.Query<dynamic>(query);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, result.GetErrorsAsString());
            Assert.IsNotNull(result.Rows);
        }

        [Test]
        public void Test_Query_HelloWorld()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = new QueryRequest("SELECT 'Hello World' AS Greeting").BaseUri(uri);

            var result = client.Query<dynamic>(query);
            Assert.IsNotNull(result);
            Assert.AreEqual("Hello World", result.Rows.First().Greeting.ToString());
        }

        [Test]
        public void Test_Query_HelloWorld_BareStringRequet()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = "SELECT 'Hello World' AS Greeting";

            var result = client.Query<dynamic>(uri, query);
            Assert.IsNotNull(result);
            Assert.AreEqual("Hello World", result.Rows.First().Greeting.ToString());
        }

        [Test]
        public void Test_Query_HelloWorld_Async()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = new QueryRequest("SELECT 'Hello World' AS Greeting").BaseUri(uri);

            var task = client.QueryAsync<dynamic>(query);
            task.Wait();

            var result = task.Result;

            Assert.IsNotNull(result);
            Assert.AreEqual("Hello World", result.Rows.First().Greeting.ToString());
        }

        [Test]
        public void Test_Query_HelloWorld_AsyncBareStringRequet()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = "SELECT 'Hello World' AS Greeting";

            var task = client.QueryAsync<dynamic>(uri, query);
            task.Wait();

            var result = task.Result;

            Assert.IsNotNull(result);
            Assert.AreEqual("Hello World", result.Rows.First().Greeting.ToString());
        }

        [Test]
        public void Test_Query_Incorrect_Syntax()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = new QueryRequest("SELECT 'Hello World' ASB Greeting").BaseUri(uri);

            var result = client.Query<dynamic>(query);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
            Assert.IsEmpty(result.Rows);
        }

        [Test]
        public void Test_Query_Select_Where_Type_Is_Beer_As_Poco()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = new QueryRequest(
                "SELECT abv, brewery_id, category, description, ibu, name, srm, style, type, upc, updated " +
                "FROM `beer-sample` as beer " +
                "WHERE beer.type='beer' LIMIT 10").BaseUri(uri);

            var result = client.Query<Beer>(query);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Rows);

            var beer = result.Rows.First();
            Assert.That(() => !string.IsNullOrEmpty(beer.Name));
        }

        [Test]
        public void Test_Query_Select_All()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = new QueryRequest("SELECT * FROM `beer-sample` as d LIMIT 10").BaseUri(uri);

            var result = client.Query<dynamic>(query);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Rows);
        }

        [Test]
        public void Test_Query_Select_All_Where_Type_Is_Beer()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var uri = new Uri(string.Format("http://{0}:8093/query", _server));
            var query = new QueryRequest("SELECT type, meta FROM `beer-sample` as d WHERE d.type='beer' LIMIT 10")
                .BaseUri(uri);

            var result = client.Query<dynamic>(query);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Rows);
        }

        [Test]
        public void Test_Query_POST_Positional_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$1 LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddPositionalParameter("beer");

            var result = client.Query<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public void Test_Query_POST_Named_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$type LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddNamedParameter("type", "beer");

            var result = client.Query<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }


        [Test]
        public async Task Test_QueryAsync_POST_Named_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$type LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddNamedParameter("type", "beer");

            var result = await client.QueryAsync<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public async Task Test_QueryAsync_POST_Positional_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$1 LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddPositionalParameter("beer");

            var result = await client.QueryAsync<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public void When_AdHoc_Is_False_Client_Uses_Prepared_Statement()
        {
            var config = new ClientConfiguration();
            var serverUri = new Uri(string.Format("http://{0}:8093/query", _server));
            const string statement = "SELECT * from `beer-sample` LIMIT 100";
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var normalRequest = QueryRequest.Create(statement).
                BaseUri(serverUri).
                Pretty(false);
            var preparedRequest = QueryRequest.Create(statement).
                AdHoc(false).
                BaseUri(serverUri).
                Pretty(false);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var resultNormal = client.Query<dynamic>(normalRequest);
            stopWatch.Stop();
            Console.WriteLine("Elasped time normal request:{0}", stopWatch.ElapsedMilliseconds);

            stopWatch = new Stopwatch();
            stopWatch.Restart();
            var resultPrepareExecute = client.Query<dynamic>(preparedRequest);
            stopWatch.Stop();
            Console.WriteLine("Elasped time prepare statement + execute 1:{0}", stopWatch.ElapsedMilliseconds);

            stopWatch = new Stopwatch();
            stopWatch.Restart();
            var resultExecute = client.Query<dynamic>(preparedRequest);
            stopWatch.Stop();
            Console.WriteLine("Elasped time execute 2:{0}", stopWatch.ElapsedMilliseconds);

            Assert.IsTrue(preparedRequest.IsPrepared);
            Assert.IsNotNull(preparedRequest.GetOriginalStatement());
            Assert.IsNotNull(preparedRequest.GetPreparedPayload());
            Assert.IsFalse(normalRequest.IsPrepared);
            Assert.AreEqual(statement, preparedRequest.GetOriginalStatement());
            Assert.AreEqual(QueryStatus.Success, resultNormal.Status, resultNormal.GetErrorsAsString());
            Assert.AreEqual(QueryStatus.Success, resultPrepareExecute.Status, resultPrepareExecute.GetErrorsAsString());
            Assert.AreEqual(QueryStatus.Success, resultExecute.Status, resultExecute.GetErrorsAsString());

            //additionally test the plan that was executed and the plan for normalRequest are the same
            var plan = client.Prepare(normalRequest).Rows.First();
            Assert.IsNotNull(preparedRequest.GetPreparedPayload());
            Assert.AreEqual(plan.Operator, preparedRequest.GetPreparedPayload().Operator);
        }

        [Test]
        public void When_AdHoc_Is_True_Client_Doesnt_Cache()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), new BucketConfig(), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` LIMIT 100").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = client.Query<dynamic>(request);
            stopWatch.Stop();
            Console.WriteLine("Elasped time 1:{0}", stopWatch.ElapsedMilliseconds);

            stopWatch = new Stopwatch();
            stopWatch.Restart();
            result = client.Query<dynamic>(request);
            stopWatch.Stop();
            Console.WriteLine("Elasped time 2:{0}", stopWatch.ElapsedMilliseconds);

            stopWatch = new Stopwatch();
            stopWatch.Restart();
            result = client.Query<dynamic>(request);
            stopWatch.Stop();
            Console.WriteLine("Elasped time 3:{0}", stopWatch.ElapsedMilliseconds);
            Assert.AreEqual(QueryStatus.Success, result.Status);
        }

        [Test]
        public void When_Identified_Error_Are_Encountered_CheckRetry_Should_Return_True()
        {
            var listErrors = new System.Collections.Generic.List<Error>();
            var error4070 = new Error()
            {
                Code = 4070,
                Message = "whatever"
            };
            var error4050 = new Error()
            {
                Code = 4050,
                Message = "whatever"
            };
            var error5000 = new Error()
            {
                Code = 5000,
                Message = "somePrefix" + QueryClient.ERROR_5000_MSG_QUERYPORT_INDEXNOTFOUND + "someSuffix"
            };
            var notAnError5000 = new Error()
            {
                Code = 5000,
                Message = "Syntax Error"
            };

            var request = new QueryRequest().AdHoc(false);
            var response = new QueryResult<string>()
            {
                Errors = listErrors,
                Success = false
            };

            listErrors.Add(error4050);
            Assert.IsTrue(QueryClient.CheckRetry(request, response));

            listErrors.Clear();
            listErrors.Add(error4070);
            Assert.IsTrue(QueryClient.CheckRetry(request, response));

            listErrors.Clear();
            listErrors.Add(error5000);
            Assert.IsTrue(QueryClient.CheckRetry(request, response));

            listErrors.Clear();
            listErrors.Add(notAnError5000);
            Assert.IsFalse(QueryClient.CheckRetry(request, response));
        }

        [Test]
        public void When_AdHoc_Is_Default_Or_True_CheckRetry_Should_Return_False()
        {
            var requestExplicit = new QueryRequest().AdHoc(true);
            var requestDefault = new QueryRequest();
            var response = new QueryResult<string>()
            {
                Success = true
            };

            Assert.IsFalse(QueryClient.CheckRetry(requestExplicit, response));
            Assert.IsFalse(QueryClient.CheckRetry(requestDefault, response));
        }

        [Test]
        public void When_AdHoc_Is_False_And_HasBeenRetried_CheckRetry_Should_Return_False()
        {
            var requestAdhocRetried = new QueryRequest().AdHoc(false);
            requestAdhocRetried.HasBeenRetried = true;
            var response = new QueryResult<string>()
            {
                Success = true
            };

            Assert.IsFalse(QueryClient.CheckRetry(requestAdhocRetried, response));
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