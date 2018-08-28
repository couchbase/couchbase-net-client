using System;
using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class QueryStringQueryTests
    {
        [Test]
        public void Boost_ReturnsStringQuery()
        {
            var query = new QueryStringQuery("description:water and some other stuff").Boost(2.2);

            Assert.IsInstanceOf<QueryStringQuery> (query);
        }

        [Test]
        public void Boost_WhenBoostIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new QueryStringQuery("description:water and some other stuff");

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Boost(-.1));
        }

        [Test]
        public void Ctor_WhenMatchIsNull_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new QueryStringQuery(null));
        }

        [Test]
        public void Export_ReturnsValidJson()
        {
            var query = new QueryStringQuery("description:water and some other stuff");

            var expected = JsonConvert.SerializeObject(new
            {
                query = "description:water and some other stuff"
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
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
