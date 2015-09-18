using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Couchbase.Tests.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseBucketSpatialViewTests
    {
        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("beer-sample"))
                {
                    var manager = bucket.CreateManager("Administrator", "password");

                    var get = manager.GetDesignDocument("beer_ext_spatial");
                    if (!get.Success)
                    {
                        var designDoc = File.ReadAllText(@"Data\\DesignDocs\\beers_ext_spatial.json");
                        var inserted = manager.InsertDesignDocument("beer_ext_spatial", designDoc);
                        if (inserted.Success)
                        {
                            Console.WriteLine("Created 'beer_ext_spatial' design doc.");
                        }
                    }
                }
            }
        }

        [Test]
        public void When_BoundaryBox_Is_Provided_Results_Are_Constrained_By_it()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("beer-sample"))
                {
                    var query = new SpatialViewQuery().From("beer_ext_spatial", "points")
                         .Stale(StaleState.False)
                         .StartRange(-10.37109375, 33.578014746143985)
                         .EndRange(43.76953125, 71.9653876991313)
                         .ConnectionTimeout(60000)
                         .Limit(1)
                         .Skip(0);

                    var result = bucket.Query<dynamic>(query);
                    Assert.AreEqual(1, result.Rows.Count());
                }
            }
        }

        [Test]
        public void When_Geometry_Is_Emitted_By_Map_Function_Geometry_Is_Not_Null()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("beer-sample"))
                {
                    var query = new SpatialViewQuery().From("beer_ext_spatial", "points")
                         .Stale(StaleState.False)
                         .ConnectionTimeout(60000)
                         .Limit(1)
                         .Skip(0);

                    var result = bucket.Query<dynamic>(query);
                    Assert.IsNotNull(result.Rows.First().Geometry);
                }
            }
        }

        [Test]
        public void When_Geometry_Is_Not_Emitted_By_Map_Function_Geometry_Is_Null()
        {
            using (var cluster = new Cluster(ClientConfigUtil.GetConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("travel-sample"))
                {
                    var query = new SpatialViewQuery().From("spatial", "routes")
                         .Stale(StaleState.False)
                         .ConnectionTimeout(60000)
                         .Limit(1)
                         .Skip(0);

                    var result = bucket.Query<dynamic>(query);
                    Assert.IsNull(result.Rows.First().Geometry);
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
