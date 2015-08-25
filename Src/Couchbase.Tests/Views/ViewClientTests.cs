using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Tests.Core.Buckets;
using Couchbase.Tests.Documents;
using Couchbase.Tests.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.Views
{
    [TestFixture]
    public class ViewClientTests
    {
        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("beer-sample"))
                {
                    var manager = bucket.CreateManager("Administrator", "password");

                    var get = manager.GetDesignDocument("beer_ext");
                    if (!get.Success)
                    {
                        var designDoc = File.ReadAllText(@"Data\\DesignDocs\\beers_ext.json");
                        var inserted = manager.InsertDesignDocument("beer_ext", designDoc);
                        if (inserted.Success)
                        {
                            Console.WriteLine("Created 'beer_ext' design doc.");
                        }
                    }
                }
            }
        }

        [Test]
        public void When_Row_Is_Dynamic_Query_By_Key_Succeeds()
        {
            var query = new ViewQuery().
             From("beer_ext", "all_beers").
             Bucket("beer-sample").
             Limit(1).
             Development(false);;

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration());

            var result = client.Execute<Beer>(query);

            var query2 = new ViewQuery().
             From("beer_ext", "all_beers").
             Bucket("beer-sample").Key(result.Rows.First().Id);

            var result2 = client.Execute<Beer>(query2);
            Assert.AreEqual(result.Rows.First().Id, result2.Rows.First().Id);
        }

        [Test]
        public void When_Poco_Is_Supplied_Map_Results_To_It()
        {
            var query = new ViewQuery().
              From("beer_ext", "all_beers").
              Bucket("beer-sample").
              Limit(10).
              Development(false);

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration());

            var result = client.Execute<Beer>(query);
            foreach (var viewRow in result.Rows)
            {
                Assert.IsNotNull(viewRow.Id);
            }
            Console.WriteLine(result.Error);
            Assert.IsNotNull(result.Rows);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(result.Rows.Count(), result.Values.Count());
        }

        [Test]
        public void When_Query_Is_Succesful_Rows_Are_Returned()
        {
            var query = new ViewQuery().
                From("beer", "brewery_beers").
                Bucket("beer-sample").
                Limit(10);

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration());

            var result = client.Execute<dynamic>(query);
            Assert.IsNotNull(result.Rows);
            foreach (var viewRow in result.Rows)
            {
                Assert.IsNotNull(viewRow.Id);
            }
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
        }

        [Test]
        public void When_View_Is_Not_Found_404_Is_Returned()
        {
            var query = new ViewQuery().
                From("beer", "view_that_does_not_exist").
                Bucket("beer-sample");

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration());

            var result = client.Execute<dynamic>(query);

            Assert.IsNotNull(result.Message);
            Assert.AreEqual(HttpStatusCode.NotFound, result.StatusCode);
            Assert.IsFalse(result.Success);

            Console.WriteLine(result.Message);
        }

        [Test]
        public void When_View_Is_Called_With_Invalid_Parameters_Error_Is_Returned()
        {
            var query = new ViewQuery().
                From("beer", "brewery_beers").
                Bucket("beer-sample").
                Group(true);

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration());

            var result = client.Execute<dynamic>(query);

            Assert.AreEqual("query_parse_error", result.Error);
            Assert.AreEqual(HttpStatusCode.BadRequest, result.StatusCode);
            Assert.IsFalse(result.Success);

            Console.WriteLine(result.Message);
        }

        [Test]
        public void When_Url_Is_Invalid_WebException_Is_Returned()
        {
            var query = new ViewQuery().
                From("beer", "brewery_beers").
                Bucket("beer-sample").
                BaseUri("http://192.168.56.105:8092/");

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration{ViewRequestTimeout = 5000});

            var result = client.Execute<dynamic>(query);
            Assert.IsNotNull(result.Rows);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, result.StatusCode);
            Assert.AreEqual(typeof(WebException), result.Exception.GetType());
        }

        [Test]
        public void When_Url_Is_Invalid_WebException_Is_Returned_2()
        {
            var query = new ViewQuery().
                From("beer", "brewery_beers").
                Bucket("beer-sample").
                BaseUri("http://192.168.62.200:8092/");

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration{ViewRequestTimeout = 5000});

            var result = client.Execute<dynamic>(query);
            Assert.IsNotNull(result.Rows);
            Assert.IsFalse(result.Success);
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, result.StatusCode);
            Assert.AreEqual(typeof(WebException), result.Exception.GetType());
        }

        [Test]
        public void Test_ExecuteAsync()
        {
            var query = new ViewQuery().
                From("docs", "all_docs").
                Bucket("default");

            var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(new ClientConfiguration()),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration());

            int n = 10000;
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4};

            Parallel.For(0, n, options, async i =>
            {
                var result = await client.ExecuteAsync<dynamic>(query);
                Console.WriteLine("{0} {1} {2}", i, result.Success, result.Message);
            });
        }

        [Test]
        public void Test_Geo_Spatial_View()
        {
            var query = new SpatialViewQuery().From("spatial", "routes")
                .BaseUri(ClientConfigUtil.GetConfiguration().Servers.First().ToString())
                .Bucket("travel-sample")
                .Stale(StaleState.False)
                .Limit(10)
                .Skip(0);

             var client = new ViewClient(new HttpClient(),
                new JsonDataMapper(ClientConfigUtil.GetConfiguration()),
                new BucketConfig { Name = "travel-sample" },
                new ClientConfiguration());

            var results = client.Execute<dynamic>(query);
            Assert.IsTrue(results.Success, results.Error);

        }
    }
}

#region [ License information ]

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