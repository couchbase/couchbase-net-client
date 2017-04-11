using System;
using Couchbase.Search.Queries.Range;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class TermRangeQueryTests
    {
        [Test]
        public void Export_ReturnsValidJson()
        {
            var query = new TermRangeQuery()
                .Min("lower", true)
                .Max("higher", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                min = "lower",
                inclusive_min = true,
                max = "higher",
                inclusive_max = true,
                field = "bar"
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Export_Omits_Min_If_Not_Provided()
        {
            var query = new TermRangeQuery()
                .Max("higher", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                max = "higher",
                inclusive_max = true,
                field = "bar"
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Export_Omits_Max_If_Not_Provided()
        {
            var query = new TermRangeQuery()
                .Min("lower", true)
                .Field("bar");

            var expected = JsonConvert.SerializeObject(new
            {
                min = "lower",
                inclusive_min = true,
                field = "bar"
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Export_Omits_Field_If_Not_Provided()
        {
            var query = new TermRangeQuery()
                .Min("lower", true)
                .Max("higher", true);

            var expected = JsonConvert.SerializeObject(new
            {
                min = "lower",
                inclusive_min = true,
                max = "higher",
                inclusive_max = true
            }, Formatting.None);

            Assert.AreEqual(expected, query.Export().ToString(Formatting.None));
        }

        [Test]
        public void Throws_InvalidOperatinException_When_Min_And_Max_Are_Not_Provided()
        {
            var query = new TermRangeQuery();
            Assert.Throws<InvalidOperationException>(() => query.Export());
        }
    }
}
