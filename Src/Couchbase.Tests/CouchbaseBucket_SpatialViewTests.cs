
using System.Runtime.InteropServices;
using Couchbase.Tests.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseBucketSpatialViewTests
    {
        [Test]
        public void Test()
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
