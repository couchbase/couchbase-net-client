using System;
using Couchbase.Search;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class ConjunctionQueryTests
    {
        [Test]
        public void Boost_ReturnsConjunctionQuery()
        {
            var query = new ConjunctionQuery().Boost(2.2);

            Assert.IsInstanceOf<ConjunctionQuery> (query);
        }

        [Test]
        public void Boost_WhenBoostIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new ConjunctionQuery();

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Boost(-.1));
        }

        [Test]
        public void Export_ReturnsValidJson()
        {
            var query = new ConjunctionQuery(
                new TermQuery("hotel").Field("type")
            );

            var result = query.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                conjuncts = new[]
                {
                    new
                    {
                        term = "hotel",
                        field = "type"
                    }
                }
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Can_create_conjunction_that_includes_query_with_boost()
        {
            new ConjunctionQuery(
                new MatchQuery("term1").Field("field1").Boost(2.0)
            );
        }

        [Test]
        public void Can_add_query_with_boost()
        {
            new ConjunctionQuery()
                .And(new MatchQuery("term1").Field("field1").Boost(2.0));
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
