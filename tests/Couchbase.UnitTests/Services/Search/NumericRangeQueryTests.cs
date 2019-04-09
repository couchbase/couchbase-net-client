using System;
using Couchbase.Services.Search.Queries.Range;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class NumericRangeQueryTests
    {
        [Fact]
        public void Boost_Returns_NumericRangeQuery()
        {
            var query = new NumericRangeQuery().Boost(2.2);

            Assert.IsType<NumericRangeQuery> (query);
        }

        [Fact]
        public void Boost_WhenBoostIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new NumericRangeQuery();

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Boost(-.1));
        }

        [Fact]
        public void Export_Returns_Valud_Json()
        {
            var query = new NumericRangeQuery()
                .Min(1)
                .Max(10)
                .Field("field");

            var expected = JsonConvert.SerializeObject(new
            {
                min = 1.0,
                inclusive_min = true,
                max = 10.0,
                inclusive_max = false,
                field = "field"
            }, Formatting.None);

            Assert.Equal(expected, query.Export().ToString(Formatting.None));
        }

        [Fact]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new NumericRangeQuery()
                .Min(1)
                .Max(10);

            var expected = JsonConvert.SerializeObject(new
            {
                min = 1.0,
                inclusive_min = true,
                max = 10.0,
                inclusive_max = false
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
