﻿using System;
using Couchbase.Search.Queries.Compound;
using Couchbase.Search.Queries.Simple;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class BooleanQueryTests
    {
        [Test]
        public void Boost_ReturnsBooleanQuery()
        {
            var query = new BooleanQuery().Boost(2.2);

            Assert.IsInstanceOf<BooleanQuery>(query);
        }

        [Test]
        public void Boost_WhenBoostIsLessThanZero_ThrowsArgumentOutOfRangeException()
        {
            var query = new BooleanQuery();

            Assert.Throws<ArgumentOutOfRangeException>(() => query.Boost(-.1));
        }

        [Test]
        public void Throws_InvalidOperationException_When_No_Sub_Queries()
        {
            var query = new BooleanQuery();

            Assert.Throws<InvalidOperationException>(() => query.Export());
        }

        [Test]
        public void Can_Execute_Query_With_Only_One_Type_Of_Sub_Query()
        {
            var query = new BooleanQuery();
            query.Must(new TermQuery("hotel").Field("type"));

            var result = query.Export();

            Assert.IsNotNull(result);
        }

        [Test]
        public void Export_Returns_Valid_Json_For_Must()
        {
            var query = new BooleanQuery();
            query.Must(new TermQuery("hotel").Field("type"));

            var result = query.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                must = new
                {
                    conjuncts = new dynamic[]
                    {
                        new
                        {
                            term = "hotel",
                            prefix_length = 0,
                            fuzziness = 0,
                            field = "type"
                        }
                    }
                }
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Export_Returns_Valid_Json_For_MustNot()
        {
            var query = new BooleanQuery();
            query.MustNot(new TermQuery("hotel").Field("type"));

            var result = query.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                must_not = new
                {
                    min = 1,
                    disjuncts = new dynamic[]
                    {
                        new
                        {
                            term = "hotel",
                            prefix_length = 0,
                            fuzziness = 0,
                            field = "type"
                        }
                    }
                }
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Export_Returns_Valid_Json_For_Should()
        {
            var query = new BooleanQuery();
            query.Should(new TermQuery("hotel").Field("type"));

            var result = query.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                should = new
                {
                    min = 1,
                    disjuncts = new dynamic[]
                    {
                        new
                        {
                            term = "hotel",
                            prefix_length = 0,
                            fuzziness = 0,
                            field = "type"
                        }
                    }
                }
            }, Formatting.None);

            Assert.AreEqual(expected, result);
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
