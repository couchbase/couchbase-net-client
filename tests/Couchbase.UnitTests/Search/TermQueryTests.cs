using System;
using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class TermQueryTests
    {
        [Fact]
        public void Boost_ReturnsFuzzyQuery()
        {
            var query = new TermQuery("theterm").Boost(2.2);

            Assert.IsType<TermQuery> (query);
        }

        [Fact]
        public void Boost_WhenBoostIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new TermQuery("theterm");

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Boost(-.1));
        }

        [Fact]
        public void Ctor_WhenMatchIsNull_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new TermQuery(null));
        }

        [Fact]
        public void PrefixLength_WhenLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new TermQuery("theterm");

            Assert.Throws<ArgumentOutOfRangeException>(() => query.PrefixLength(-1));
        }

        [Fact]
        public void Fuzziness_WhenLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new TermQuery("theterm");

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Fuzziness(-1));
        }

        [Fact]
        public void Export_ReturnsValidJson()
        {
            var query = new TermQuery("theterm")
                .Field("field")
                .PrefixLength(5)
                .Fuzziness(1);

            var expected = JsonConvert.SerializeObject(new
            {
                term = "theterm",
                prefix_length = 5,
                fuzziness = 1,
                field = "field"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new TermQuery("theterm")
                .PrefixLength(5)
                .Fuzziness(1);

            var expected = JsonConvert.SerializeObject(new
            {
                term = "theterm",
                prefix_length = 5,
                fuzziness = 1
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
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
