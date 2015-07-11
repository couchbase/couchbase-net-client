using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using Couchbase.Configuration.Client;
using Couchbase.N1QL;
using Couchbase.Tests.Documents;
using Couchbase.Views;
using Newtonsoft.Json.Bson;
using NUnit.Framework;

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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Rows);
        }

        [Test]
        public void Test_Query_HelloWorld()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            Assert.IsNotNullOrEmpty(beer.Name);
        }

        [Test]
        public void Test_Query_Select_All()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$1 LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddPositionalParameter("beer").
                HttpMethod(Method.Post);

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
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$type LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddNamedParameter("type", "beer").
                HttpMethod(Method.Post);

            var result = client.Query<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public void Test_Query_GET_Positional_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$1 LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddPositionalParameter("beer").
                HttpMethod(Method.Get);

            var result = client.Query<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public void Test_Query_GET_Named_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$type LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddNamedParameter("type", "beer").
                HttpMethod(Method.Get);

            var result = client.Query<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public async void Test_QueryAsync_POST_Named_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$type LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddNamedParameter("type", "beer").
                HttpMethod(Method.Post);

            var result = await client.QueryAsync<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public async void Test_QueryAsync_GET_Positional_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$1 LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddPositionalParameter("beer").
                HttpMethod(Method.Get);

            var result = await client.QueryAsync<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public async void Test_QueryAsync_GET_Named_Parameters()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var request = QueryRequest.Create("SELECT * from `beer-sample` WHERE type=$type LIMIT 10").
                BaseUri(new Uri(string.Format("http://{0}:8093/query", _server))).
                Pretty(false).
                AddNamedParameter("type", "beer").
                HttpMethod(Method.Get);

            var result = await client.QueryAsync<dynamic>(request);
            Assert.AreEqual(QueryStatus.Success, result.Status);
            Assert.AreEqual(10, result.Rows.Count);
            foreach (var row in result.Rows)
            {
                Console.WriteLine(row);
            }
        }

        [Test]
        public void When_Prepared_Is_True_Client_Uses_Prepared_Statement()
        {
            var config = new ClientConfiguration();
            var serverUri = new Uri(string.Format("http://{0}:8093/query", _server));
            const string statement = "SELECT * from `beer-sample` LIMIT 100";
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
            var normalRequest = QueryRequest.Create(statement).
                BaseUri(serverUri).
                Pretty(false);
            var preparedRequest = QueryRequest.Create(). //will set the plan later
                BaseUri(serverUri).
                Pretty(false);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var resultNormal = client.Query<dynamic>(normalRequest);
            stopWatch.Stop();
            Console.WriteLine("Elasped time normal request:{0}", stopWatch.ElapsedMilliseconds);

            stopWatch = new Stopwatch();
            stopWatch.Restart();
            var plan = client.Prepare(normalRequest).Rows.First();
            preparedRequest.Prepared(plan).
                BaseUri(serverUri).
                Pretty(false);
            var resultPrepareExecute = client.Query<dynamic>(preparedRequest);
            stopWatch.Stop();
            Console.WriteLine("Elasped time prepare statement + execute 1:{0}", stopWatch.ElapsedMilliseconds);

            stopWatch = new Stopwatch();
            stopWatch.Restart();
            var resultExecute = client.Query<dynamic>(preparedRequest);
            stopWatch.Stop();
            Console.WriteLine("Elasped time execute 2:{0}", stopWatch.ElapsedMilliseconds);

            Assert.IsTrue(preparedRequest.IsPrepared);
            Assert.IsFalse(normalRequest.IsPrepared);
            Assert.AreEqual(QueryStatus.Success, resultNormal.Status);
            Assert.AreEqual(QueryStatus.Success, resultPrepareExecute.Status);
            Assert.AreEqual(QueryStatus.Success, resultExecute.Status);
        }

        [Test]
        public void When_Prepared_Is_False_Client_Doesnt_Cache()
        {
            var config = new ClientConfiguration();
            var client = new QueryClient(new HttpClient(), new JsonDataMapper(config), config);
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