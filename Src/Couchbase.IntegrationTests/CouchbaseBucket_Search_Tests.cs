using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    [TestFixture]
    public class CouchbaseBucketSearchTests
    {
        [Test]
        public void Test_Sync()
        {
            using (var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("travel-sample"))
                {
                     var query = new MatchQuery("inn");
                     var results = bucket.Query(new SearchQuery
                     {
                         Index = "idx_travel",
                         Query = query
                     }.Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)));

                     Assert.IsTrue(results.Success);
                }
            }
        }

        [Test]
        public async Task Test_Async()
        {
            using (var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("travel-sample"))
                {
                    var query = new MatchQuery("inn");

                    var results = await bucket.QueryAsync(new SearchQuery
                    {
                        Index = "idx_travel",
                        Query = query
                    }.Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)));

                    Assert.IsTrue(results.Success);
                }
            }
        }

        [Test]
        public void Test_Sync_failed()
        {
            using (var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("travel-sample"))
                {
                    var query = new MatchQuery("inn");
                    var results = bucket.Query(new SearchQuery
                    {
                        Index = "id_travel",
                        Query = query
                    }.Limit(10).Timeout(TimeSpan.FromMilliseconds(10000)));

                    Assert.IsFalse(results.Success);
                }
            }
        }

        [Test]
        public void Facets_Success()
        {
            using (var cluster = new Cluster(TestConfiguration.GetCurrentConfiguration()))
            {
                using (var bucket = cluster.OpenBucket("travel-sample"))
                {
                    var results = bucket.Query(new SearchQuery
                    {
                        Index = "id_travel",
                        Query = new MatchQuery("inn")
                    }.Facets(
                        new TermFacet("termfacet", "thefield", 10),
                        new DateRangeFacet("daterangefacet", "thefield", 10).AddRange(DateTime.Now, DateTime.Now.AddDays(1)),
                        new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange(2.2f, 3.5f)));

                    Assert.IsFalse(results.Success);
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
