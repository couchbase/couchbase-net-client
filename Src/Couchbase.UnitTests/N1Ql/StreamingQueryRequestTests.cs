using System;
using System.Linq;
using Couchbase.N1QL;
using NUnit.Framework;
using Newtonsoft.Json;

namespace Couchbase.UnitTests.N1Ql
{
    [TestFixture]
    public class StreamingQueryRequestTests
    {
        [Test]
        public void Test_Count_AfterForceRead()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };
            response.ForceRead();
            Assert.AreEqual(10, response.Count());
        }

        [Test]
        public void Test_Any()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.IsTrue(response.Any());
        }

        [Test]
        public void Test_Any_AfterForceRead()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };
            response.ForceRead();
            Assert.IsTrue(response.Any());
        }

        [Test]
        public void Test_Count()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.AreEqual(10, response.Count());
        }

        [Test]
        public void Test_First()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            var first = response.First();
            Assert.IsNotNull(first);
        }

        [Test]
        public void Test_First_AfterForceRead()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };
            response.ForceRead();

            var first = response.First();
            Assert.IsNotNull(first);
        }

        [Test]
        public void Test_SecondEnumeration_AfterRegularRead_ThrowsStreamAlreadyReadException()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            //read the results
            var count = 0;
            foreach (var beer in response)
            {
                count++;
            }
            Assert.AreEqual(10, count);

            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            Assert.Throws<StreamAlreadyReadException>(() => response.ToList());
        }

        [Test]
        public void Test_SecondEnumeration_AfterError_ThrowsStreamAlreadyReadException()
        {
            // For consistency in behavior, enumerating the results twice should throw an exception
            // if ForceRead wasn't used, even when there was no pause to read the results property.

            var stream = ResourceHelper.ReadResourceAsStream("Data\\errors_and_warnings.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            //read the results
            var count = 0;
            foreach (var beer in response)
            {
                count++;
            }
            Assert.AreEqual(0, count);

            // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
            Assert.Throws<StreamAlreadyReadException>(() => response.ToList());
        }

        [Test]
        public void Test_Enumeration_After_ForceRead()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };
            response.ForceRead();

            //read the header
            Assert.AreEqual(QueryStatus.Success, response.Status);

            //read the results
            var count = 0;
            foreach (var beer in response.Rows)
            {
                count++;
            }
            Assert.AreEqual(10, count);
        }

        [Test]
        public void Test_Status_SuccessfulQuery_BeforeEnumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.AreEqual(QueryStatus.Success, response.Status);
        }

        [Test]
        public void Test_Status_SuccessfulQuery_AfterEnumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            // ReSharper disable once UnusedVariable
            var temp = response.ToList();

            Assert.AreEqual(QueryStatus.Success, response.Status);
        }

        [Test]
        public void Test_Status_ErrorQuery()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\errors_and_warnings.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.AreNotEqual(QueryStatus.Success, response.Status);
        }

        [Test]
        public void Test_Success_SuccessfulQuery_BeforeEnumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.AreEqual(true, response.Success);
        }

        [Test]
        public void Test_Success_SuccessfulQuery_AfterEnumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            // ReSharper disable once UnusedVariable
            var temp = response.ToList();

            Assert.AreEqual(true, response.Success);
        }

        [Test]
        public void Test_Success_ErrorQuery()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\errors_and_warnings.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.AreEqual(false, response.Success);
        }

        [Test]
        public void Test_ClientContextID()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.AreEqual("7::8", response.ClientContextId);
        }

        [Test]
        public void Test_RequestID()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };

            Assert.AreEqual("ca692d83-1e09-4a87-ab66-cfd9f2c4a898", response.RequestId.ToString());
        }

        [Test]
        public void Test_Metrics()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\n1ql-response.json");
            var response = new StreamingQueryResult<Beer>
            {
                ResponseStream = stream
            };
            response.ForceRead();

            Assert.AreEqual("90.385425ms", response.Metrics.ElaspedTime);
            Assert.AreEqual("90.322896ms", response.Metrics.ExecutionTime);
            Assert.AreEqual(10, response.Metrics.ResultCount);
            Assert.AreEqual(7262, response.Metrics.ResultSize);
        }

        [Test]
        public void Test_Enumeration_Chunked()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\chunked.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            foreach (var item in response)
            {
               Assert.IsNotNull(item);
            }
        }

        [Test]
        public void Test_Enumeration_Warnings()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\errors_and_warnings.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            Assert.IsNotEmpty(response.Warnings);
        }

        [Test]
        public void Test_Enumeration_Errors()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\errors_and_warnings.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            Assert.IsNotEmpty(response.Errors);
        }

        [Test]
        public void Test_Enumeration_NoClientId()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\no_client_id.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            Assert.IsNull(response.ClientContextId);
        }

        [Test]
        public void Test_Enumeration_NoPretty()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\no_pretty.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            foreach (var item in response)
            {
                Assert.IsNotNull(item);
            }
        }

        [Test]
        public void Test_Enumeration_ZeroResults()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\success_0.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            Assert.IsFalse(response.Any());
        }

        [Test]
        public void Test_Enumeration_Breweries()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\success_1.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            Assert.IsTrue(response.Any());
        }

        [Test]
        public void Test_Enumeration_BreweriesAndBeers()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\success_5.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            Assert.IsTrue(response.Any());
        }


        [Test]
        public void Test_Enumeration_ParseError()
        {
            var stream = ResourceHelper.ReadResourceAsStream("Data\\parse_error.json");

            var response = new StreamingQueryResult<dynamic>
            {
                ResponseStream = stream
            };

            response.ForceRead();

            Assert.AreEqual(response.Errors.First().Name, "parse_error");
        }

        public class Beer : Document<Beer>
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("abv")]
            public decimal Abv { get; set; }

            [JsonProperty("ibu")]
            public decimal Ibu { get; set; }

            [JsonProperty("srm")]
            public decimal Srm { get; set; }

            [JsonProperty("upc")]
            public int Upc { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("brewery_id")]
            public string BreweryId { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("style")]
            public string Style { get; set; }

            [JsonProperty("category")]
            public string Category { get; set; }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
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
