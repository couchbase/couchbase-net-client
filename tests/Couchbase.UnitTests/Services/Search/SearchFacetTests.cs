using System;
using Couchbase.Search;
using Xunit;

namespace Couchbase.UnitTests.Services.Search
{
    public class SearchFacetTests
    {
        [Fact]
        public void ToString_TermFacet()
        {
            var term = new TermFacet
            {
                Field = "fieldName",
                Name = "myTermFacet",
                Size = 3
            };

            var expected = "\"myTermFacet\":{\"field\":\"fieldName\",\"size\":3}";
            var actual = term.ToString().Replace(Environment.NewLine, string.Empty).Replace(" ", string.Empty);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToString_NumericRangeFacet()
        {
            var term = new NumericRangeFacet
            {
                Field = "fieldName",
                Name = "myNumericFacet",
                Size = 2,
            }.AddRanges(
                new Range<float>{ Name = "range1", Start = 0.1F, End = 3.0F },
                new Range<float> {Name = "range2", Start = 3.1F});

            var expected = "\"myNumericFacet\":{\"field\":\"fieldName\",\"size\":2,\"numeric_ranges\":[{\"name\":\"range1\",\"min\":0.1,\"max\":3.0},{\"name\":\"range2\",\"min\":3.1}]}";
            var actual = term.ToString().Replace(Environment.NewLine, string.Empty).Replace(" ", string.Empty);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ToString_DateRangeFacet()
        {
            var term = new DateRangeFacet
            {
                Field = "fieldName",
                Name = "myDateFacet",
                Size = 2,
            }.AddRanges(
                new Range<DateTime> {Name = "old", End = new DateTime(2016, 01, 01)},
                new Range<DateTime> {Name = "thisYear", Start = new DateTime(2016, 01, 01, 0, 0, 1)},
                new Range<DateTime> {Name = "theYear2011", Start = new DateTime(2011, 01, 01), End = new DateTime(2011, 12, 31, 23,59,59)});

            var expected = "\"myDateFacet\":{\"field\":\"fieldName\",\"size\":2,\"date_ranges\":" +
                "[{\"name\":\"old\",\"end\":\"2016-01-01T00:00:00\"}," +
                "{\"name\":\"thisYear\",\"start\":\"2016-01-01T00:00:01\"}," +
                "{\"name\":\"theYear2011\",\"start\":\"2011-01-01T00:00:00\",\"end\":\"2011-12-31T23:59:59\"}]}";

            var actual = term.ToString().Replace(Environment.NewLine, string.Empty).Replace(" ", string.Empty);
            Assert.Equal(expected, actual);
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
