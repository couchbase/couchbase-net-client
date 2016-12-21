using System.Linq;
using System.Net;
using Couchbase.UnitTests.N1Ql;
using Couchbase.Views;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Views
{
    [TestFixture]
    public class StreamingViewResultTests
    {
        [Test]
        public void Test_Count()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            Assert.AreEqual(10, response.Rows.Count());
        }

        [Test]
        public void Test_Any()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            Assert.IsTrue(response.Rows.Any());
        }

        [Test]
        public void Test_First()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            var first = response.Rows.First();
            Assert.IsNotNull(first);
        }

        [Test]
        public void Test_Enumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            //read the results
            var count = 0;
            foreach (var beer in response.Rows)
            {
                count++;
            }
            Assert.AreEqual(10, count);
        }

        [Test]
        public void Test_Repeat_Enumeration()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            Assert.AreEqual(10, response.Rows.ToList().Count);
            Assert.Throws<StreamAlreadyReadException>(() => response.Rows.ToList());
        }

        [Test]
        public void Test_Values()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            Assert.AreEqual(10, response.Values.Count());
        }

        [Test]
        public void Test_Success()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            Assert.AreEqual(true, response.Success);
        }

        [Test]
        public void Test_StatusCode()
        {
            const HttpStatusCode statusCode = HttpStatusCode.Accepted;
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, statusCode, string.Empty, stream);

            Assert.AreEqual(statusCode, response.StatusCode);
        }

        [Test]
        public void Test_Message()
        {
            const string message = "message";
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, message, stream);

            Assert.AreEqual(message, response.Message);
        }

        [Test]
        public void Test_TotalRows()
        {
            var stream = ResourceHelper.ReadResourceAsStream(@"Data\view_result.json");
            var response = new StreamingViewResult<Beer>(true, HttpStatusCode.OK, string.Empty, stream);

            Assert.AreEqual(7303, response.TotalRows);
        }
    }

    public class Beer : Document<StreamingQueryRequestTests.Beer>
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
