using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Views;
using NUnit.Framework;
using Wintellect;

namespace Couchbase.Tests.Views
{
    [TestFixture]
    public class ViewClientTests
    {
        [Test]
        public void When_Query_Is_Succesful_Rows_Are_Returned()
        {
            var query = new ViewQuery().
                From("beer", "brewery_beers").
                Bucket("beer-sample");

            var client = new ViewClient(new HttpClient(), new JsonDataMapper(), new BucketConfig{Name = "beer-sample"}, new ClientConfiguration());
            var result = client.Execute<dynamic>(query);
            Assert.IsNotNull(result.Rows);
            Assert.AreEqual(result.StatusCode, HttpStatusCode.OK);
        }

        [Test]
        public void When_View_Is_Not_Found_404_Is_Returned()
        {
            var query = new ViewQuery().
                From("beer", "view_that_does_not_exist").
                Bucket("beer-sample");

            var client = new ViewClient(new HttpClient(), new JsonDataMapper(), new BucketConfig { Name = "beer-sample" }, new ClientConfiguration());
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

            var client = new ViewClient(new HttpClient(), new JsonDataMapper(), new BucketConfig { Name = "beer-sample" }, new ClientConfiguration());
            var result = client.Execute<dynamic>(query);

            Assert.AreEqual("The remote server returned an error: (400) Bad Request.", result.Error);
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
                new JsonDataMapper(),
                new BucketConfig { Name = "beer-sample" },
                new ClientConfiguration{ViewRequestTimeout = 30000});

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

            var client = new ViewClient(new HttpClient(), new JsonDataMapper(), new BucketConfig { Name = "beer-sample" }, new ClientConfiguration());

            int n = 10000;
            var options = new ParallelOptions { MaxDegreeOfParallelism = 4};

            Parallel.For(0, n, options, async i =>
            {
                var result = await client.ExecuteAsync<dynamic>(query);
                Console.WriteLine("{0} {1} {2}", i, result.Success, result.Message);
            });
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