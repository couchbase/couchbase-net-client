using System;
using Couchbase.Search.Queries.Range;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Search
{
    public class DateRangeQueryTests
    {
        [Fact]
        public void Boost_ReturnsDateRangeQuery()
        {
            var query = new DateRangeQuery().Boost(2.2);

            Assert.IsType<DateRangeQuery>(query);
        }

        [Fact]
        public void Boost_WhenBoostIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new DateRangeQuery();

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Boost(-.1));
        }

        [Fact]
        public void Throws_Exception_If_Start_And_End_Are_Not_Provided_When_Export_Is_Called()
        {
            var query = new DateRangeQuery();

            Assert.Throws<InvalidOperationException>(() => query.Export());
        }

        [Fact]
        public void Export_Returns_Valid_Json()
        {
            var start = DateTime.Today;
            var end = DateTime.Now;
            var query = new DateRangeQuery()
                .Start(start)
                .End(end)
                .Field("created_at")
                .Parser("parser");

            var expected = JsonConvert.SerializeObject(new
            {
                start = start,
                inclusive_start = true,
                end = end,
                inclusive_end = false,
                datetime_parser = "parser",
                field = "created_at"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var start = DateTime.Today;
            var end = DateTime.Now;
            var query = new DateRangeQuery()
                .Start(start)
                .End(end)
                .Parser("parser");

            var expected = JsonConvert.SerializeObject(new
            {
                start = start,
                inclusive_start = true,
                end = end,
                inclusive_end = false,
                datetime_parser = "parser",
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
