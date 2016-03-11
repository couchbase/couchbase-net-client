
using System;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Search.Queries;
using Couchbase.Search.Queries.Simple;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class MatchQueryQueryTests
    {
        [Test]
        public void Boost_ReturnsPrefixQuery()
        {
            var query = new MatchQuery("somematchquery").Boost(2.2);

            Assert.IsInstanceOf<MatchQuery> (query);
        }

        [Test]
        public void Boost_WhenBoostIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new MatchQuery("somematchquery");

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Boost(-.1));
        }

        [Test]
        public void Ctor_WhenMatchIsNull_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(()=> new MatchQuery(null));
        }

        [Test]
        public void PrefixLength_WhenLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new MatchQuery("theterm");

            Assert.Throws<ArgumentOutOfRangeException>(() => query.PrefixLength(-1));
        }

        [Test]
        public void Fuzziness_WhenLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new MatchQuery("theterm");

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Fuzziness(-1));
        }

        [Test]
        public void Export_ReturnsValidJson()
        {
            var query = new MatchQuery("somematchquery").Boost(2.2);
            var expected = "{\"ctl\":{\"timeout\":10000,\"consistency\":{\"level\":\"at_plus\",\"vectors\":{}}},\"size\":10,\"from\":20,\"highlight\":{\"style\":null,\"fields\":null},\"fields\":[\"*\"],\"facets\":null,\"explain\":true,\"query\":{\"query\":\"somematchquery\",\"boost\":2.2}}";
            var actual = query.Export(new SearchParams().
                Skip(20).
                Limit(10).Explain(true).
                Timeout(TimeSpan.FromMilliseconds(10000)).
                WithConsistency(ScanConsistency.AtPlus));

            Assert.AreEqual(expected, actual.ToString().Replace("\r\n", "").Replace(" ", ""));
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
