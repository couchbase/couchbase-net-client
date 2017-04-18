using Couchbase.Search.Sort;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Search
{
    [TestFixture]
    public class FieldSearchSortTests
    {
        [Test]
        public void Outputs_Valid_Json()
        {
            var sort = new FieldSearchSort("foo", FieldType.String, FieldMode.Min, FieldMissing.First, true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "field",
                decending = true,
                field = "foo",
                type = "string",
                mode = "min",
                missing = "first"
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Omits_Type_If_Auto()
        {
            var sort = new FieldSearchSort("foo", FieldType.String, FieldMode.Min, FieldMissing.First);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "field",
                field = "foo",
                type = "string",
                mode = "min",
                missing = "first"
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Omits_Mode_If_Default()
        {
            var sort = new FieldSearchSort("foo", FieldType.Auto, FieldMode.Min, FieldMissing.First, true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "field",
                decending = true,
                field = "foo",
                mode = "min",
                missing = "first"
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Omits_Missing_If_Last()
        {
            var sort = new FieldSearchSort("foo", FieldType.String, FieldMode.Default, FieldMissing.First, true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "field",
                decending = true,
                field = "foo",
                type = "string",
                missing = "first"
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Omits_Decending_If_False()
        {
            var sort = new FieldSearchSort("foo", FieldType.String, FieldMode.Min, FieldMissing.Last, true);
            var result = sort.Export().ToString(Formatting.None);

            var expected = JsonConvert.SerializeObject(new
            {
                by = "field",
                decending = true,
                field = "foo",
                type = "string",
                mode = "min"
            }, Formatting.None);

            Assert.AreEqual(expected, result);
        }
    }
}