using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Tests.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.Views
{
    [TestFixture]
    public class SpatialViewQueryTests
    {
        [Test]
        public void Test_RawUri()
        {
            const string expected = "http://localhost:8092/default/_design/testdoc/_spatial/testview?";
            var query = new SpatialViewQuery().From("testdoc", "testview");
            Assert.AreEqual(expected, query.RawUri().ToString());
        }

        [Test]
        public void When_UseSsl_Is_True_RawUri_Returns_Https()
        {
            const string expected = "https://localhost:18092/default/_design/testdoc/_spatial/testview?";
            var query = new SpatialViewQuery().From("testdoc", "testview");
            query.UseSsl = true;

            Assert.AreEqual(expected, query.RawUri().ToString());
        }

        [Test]
        public void When_Bucket_Is_Set_RawUri_Returns_BucketName_In_Uri()
        {
            const string expected = "http://localhost:8092/foo/_design/testdoc/_spatial/testview?";
            var query = (SpatialViewQuery)new SpatialViewQuery().From("testdoc", "testview");
            query.BucketName = "foo";

            Assert.AreEqual(expected, query.RawUri().ToString());
        }

        [Test]
        public void When_Port_Is_Set_RawUri_Returns_Custom_Port_In_Uri()
        {
            const string expected = "http://localhost:9999/default/_design/testdoc/_spatial/testview?";
            var query = (SpatialViewQuery)new SpatialViewQuery().From("testdoc", "testview");
            query.Port = 9999;

            Assert.AreEqual(expected, query.RawUri().ToString());
        }

        [Test]
        public void When_QueryParams_Are_Set_RawUri_Adds_Them_To_Uri()
        {
            const string queryParams = @"/travel-sample/_design/spatial/_spatial/routes?stale=false&connection_timeout=60000&limit=10&skip=0";

            var query = new SpatialViewQuery().From("spatial", "routes")
                .Bucket("travel-sample")
                .Stale(StaleState.False)
                .ConnectionTimeout(60000)
                .Limit(10)
                .Skip(0);
            var uri = query.RawUri();
            Assert.AreEqual(queryParams, uri.PathAndQuery);
        }

        [Test]
        public void When_StartRange_And_EndRange_Are_Set_They_Are_Added_To_Uri()
        {
            const string queryParams = @"/travel-sample/_design/spatial/_spatial/routes?stale=false&connection_timeout=60000&limit=10&skip=0&start_range=[null,null,10000.0]&end_range=[null,null,null]";

            var query = new SpatialViewQuery().From("spatial", "routes")
                .Bucket("travel-sample")
                .Stale(StaleState.False)
                .ConnectionTimeout(60000)
                .StartRange(new List<double?>{null, null, 10000})
                .EndRange(new List<double?>{null, null, null})
                .Limit(10)
                .Skip(0);
            var uri = query.RawUri();
            Assert.AreEqual(queryParams, uri.PathAndQuery);
        }

        [Test]
        public void When_IBucket_Executes_Query_Uri_Is_Properly_Formed()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("travel-sample"))
                {
                    var query = new SpatialViewQuery().From("spatial", "routes")
                         .Bucket("travel-sample")
                         .Stale(StaleState.False)
                         .ConnectionTimeout(60000)
                         .Limit(10)
                         .Skip(0);

                    var result = bucket.Query<dynamic>(query);
                    var uri = query.RawUri();

                    var expected =
                        "/travel-sample/_design/spatial/_spatial/routes?stale=false&connection_timeout=60000&limit=10&skip=0";
                    Assert.AreEqual(expected, uri.PathAndQuery);
                }
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
