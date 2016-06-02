using System;
using Couchbase.N1QL;
using Couchbase.Search;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class SearchParamsTests
    {
        [Test]
        public void ToJson_JsonStringIsValid()
        {
            var searchParams = new SearchQuery().
                Skip(20).
                Limit(10).Explain(true).
                Timeout(TimeSpan.FromMilliseconds(10000)).
                WithConsistency(ScanConsistency.AtPlus);

            //var expected = "{\"ctl\":{\"timeout\":10000,\"consistency\":{\"level\":\"at_plus\",\"vectors\":{\"customerIndex\":{\"0\":123,\"1/a0b1c2\":234}}}},\"query\":{\"query\":\"alice smith\",\"boost\": 1},\"size\": 10,\"from\":20,\"highlight\":{\"style\": null,\"fields\":null},\"fields\":[\"*\"],\"facets\":null,\"explain\":true}";
            var expected = "{\"ctl\":{\"timeout\":10000,\"consistency\":{\"level\":\"at_plus\",\"vectors\":{}}},\"size\":10,\"from\":20,\"explain\":true}";
            var actual = searchParams.ToJson().ToString().Replace("\r\n", "").Replace(" ", "");
            Console.WriteLine(actual);
            Console.WriteLine(expected);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ToJson_WithFacets()
        {
            var searchParams = new SearchQuery().Facets(
                new TermFacet("termfacet", "thefield", 10),
                new DateRangeFacet("daterangefacet", "thefield", 10).AddRange(DateTime.Now, DateTime.Now.AddDays(1)),
                new NumericRangeFacet("numericrangefacet", "thefield", 2).AddRange(2.2f, 3.5f));

            Console.WriteLine(searchParams.ToJson());
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
