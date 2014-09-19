using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class OperationBaseTests : OperationTestBase
    {
        [Test]
        public void When_Type_Is_Int_DateFormat_Is_Json()
        {
            const string key = "OperationBaseTests.When_Type_Is_Int_DateFormat_Is_Json";
            var set = new Set<int?>(key, 100, GetVBucket(), new AutoByteConverter());
            var result = IOStrategy.Execute(set);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(set.Format, DataFormat.Json);

            var get = new Get<int>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(DataFormat.Json, get.Format);
            Assert.AreEqual(Compression.None, get.Compression);
        }

        [Test]
        public void When_Type_Is_String_DateFormat_Is_String()
        {
            const string key = "OperationBaseTests.When_Type_Is_String_DateFormat_Is_String";
            var set = new Set<string>(key, "somestring", GetVBucket(), new AutoByteConverter());
            var result = IOStrategy.Execute(set);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(set.Format, DataFormat.String);

            var get = new Get<string>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(DataFormat.String, get.Format);
            Assert.AreEqual(Compression.None, get.Compression);
        }

        [Test]
        public void When_Type_Object_Int_DateFormat_Is_Json()
        {
            const string key = "OperationBaseTests.When_Type_Object_Int_DateFormat_Is_Json";
            var value = new
            {
                Name = "name",
                Foo = "foo"
            };

            var set = new Set<dynamic>(key, value, GetVBucket(), new AutoByteConverter());
            var result = IOStrategy.Execute(set);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(set.Format, DataFormat.Json);

            var get = new Get<dynamic>(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);

            Assert.IsTrue(getResult.Success);
            Assert.AreEqual(DataFormat.Json, get.Format);
            Assert.AreEqual(Compression.None, get.Compression);
        }

        [Test]
        public void Test_ReadExtras_When_Type_Is_Binary()
        {
            var key = "binkey";
            var expected = new byte[]
            {
                0x81, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x02, 0x00, 0x04, 0x0e, 0xa2, 0x9d, 0x32, 0xdb, 0xb5, 0x03, 0x00, 0x00, 0x02, 0x01, 0x02, 0x03, 0x04
            };

            var get = new Get<byte[]>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            get.ReadExtras(expected);
            Assert.AreEqual(DataFormat.Binary, get.Format);
        }

        [Test]
        public void Test_CreateExtras_When_Type_Is_Binary()
        {
            var key = "binkey";
            var expected = new byte[]
            {
                0x03, 0x00, 0x00, 0x02, 00, 00, 00, 00
            };

            var value = new byte[] { 1, 2, 3, 4 };
            var set = new Set<byte[]>(key, value, GetVBucket(), new AutoByteConverter());
            var bytes = set.CreateExtras();

            Assert.AreEqual(expected[0], bytes[0]);
            Assert.AreEqual(DataFormat.Binary, set.Format);
        }

        [Test]
        public void Test_When_Type_Is_Binary_Integrated()
        {
            var key = "binkey";

            var value = new byte[] {1, 2, 3, 4};
            var set = new Set<byte[]>(key, value, GetVBucket(), new AutoByteConverter());
            var setResult = IOStrategy.Execute(set);
            Assert.IsTrue(setResult.Success);
            Assert.AreEqual(DataFormat.Binary, set.Format);

            var get = new Get<byte[]>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);
            Assert.IsTrue(getResult.Success);

            Assert.AreEqual(DataFormat.Binary, get.Format);
        }

        [Test]
        public void Test_When_Type_Is_Json_Integrated()
        {
            var key = "jsonkey";

            var value = new { x = "hi", y = 14 };
            var set = new Set<dynamic>(key, value, GetVBucket(), new AutoByteConverter());
            var setResult = IOStrategy.Execute(set);
            Assert.IsTrue(setResult.Success);
            Assert.AreEqual(DataFormat.Json, set.Format);

            var get = new Get<dynamic>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);
            Assert.AreEqual(DataFormat.Json, get.Format);
            Assert.IsTrue(getResult.Success);
        }

        [Test]
        public void Test_When_Type_Is_String_Integrated()
        {
            var key = "stringkey";

            var value = "hiho";
            var set = new Set<string>(key, value, GetVBucket(), new AutoByteConverter());
            var setResult = IOStrategy.Execute(set);
            Assert.IsTrue(setResult.Success);
            Assert.AreEqual(DataFormat.String, set.Format);

            var get = new Get<string>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);
            Assert.AreEqual(DataFormat.String, get.Format);
            Assert.IsTrue(getResult.Success);
        }

        [Test]
        public void Test_When_Type_Is_Int_Integrated()
        {
            var key = "intkey";

            var value = 14;
            var set = new Set<int?>(key, value, GetVBucket(), new AutoByteConverter());
            var setResult = IOStrategy.Execute(set);
            Assert.IsTrue(setResult.Success);
            Assert.AreEqual(DataFormat.Json, set.Format);

            var get = new Get<int?>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);
            Assert.AreEqual(DataFormat.Json, get.Format);
            Assert.IsTrue(getResult.Success);
        }

        [Test]
        public void Test_When_Type_Is_Number_Integrated()
        {
            var key = "intkey";

            var value = 14.666m;
            var set = new Set<decimal?>(key, value, GetVBucket(), new AutoByteConverter());
            var setResult = IOStrategy.Execute(set);
            Assert.IsTrue(setResult.Success);
            Assert.AreEqual(DataFormat.Json, set.Format);

            var get = new Get<decimal?>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            var getResult = IOStrategy.Execute(get);
            Assert.AreEqual(DataFormat.Json, get.Format);
            Assert.IsTrue(getResult.Success);
        }

        [Test]
        public void Test_When_Type_Is_Json()
        {
            var key = "jsonkey";
            var expected = new byte[]
            {
                0x81, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x15, 0x00, 0x00, 0x00, 0x02, 0x00, 0x04, 0x0e, 0x9f, 0x43, 0xcf, 0xbd, 0x33, 0x02, 0x00, 0x00, 0x00, 0x7b, 0x22, 0x78, 0x22, 0x3a, 0x22, 0x68, 0x69, 0x22, 0x2c, 0x22, 0x79, 0x22, 0x3a, 0x31, 0x34, 0x7d
            };

            var get = new Get<dynamic>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            get.ReadExtras(expected);
            Assert.AreEqual(DataFormat.Json, get.Format);
        }

        [Test]
        public void Test_CreateExtras_When_Type_Is_Json()
        {
            var key = "jsonkey";
            var expected = new byte[]
            {
                0x02, 0x00, 0x00, 0x00
            };

            var value = new {x="hi",y=14};
            var set = new Set<dynamic>(key, value, GetVBucket(), new AutoByteConverter());
            var bytes = set.CreateExtras();

            Assert.AreEqual(expected[0], bytes[0]);
            Assert.AreEqual(DataFormat.Json, set.Format);
        }

        [Test]
        public void Test_When_Type_Is_String()
        {
            var key = "stringkey";
            var expected = new byte[]
            {
              81, 00, 00, 00, 04, 00, 00, 00, 00, 00, 00, 08, 00, 00, 00, 04, 00, 04, 0x0e, 0x9f, 43, 0xe8, 0x4b, 0xd4, 04, 00, 00, 04, 68, 69, 68, 0x6f
            };

            var get = new Get<string>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            get.ReadExtras(expected);
            Assert.AreEqual(DataFormat.String, get.Format);
        }

        [Test]
        public void Test_CreateExtras_When_Type_Is_String()
        {
            var key = "stringkey";
            var expected = new byte[]
            {
                0x04, 0x00, 0x00, 0x04
            };

            var value = "hiho";
            var set = new Set<string>(key, value, GetVBucket(), new AutoByteConverter());
            var bytes = set.CreateExtras();

            Assert.AreEqual(expected[0], bytes[0]);
            Assert.AreEqual(DataFormat.String, set.Format);
        }


        [Test]
        public void Test_When_Type_Is_Number()
        {
            var key = "numberkey";
            var expected = new byte[]
            {
                81, 00, 00, 00, 04, 00, 00, 00, 00, 00, 00, 0x0a, 00, 00, 00, 02, 00, 05, 0x4a, 10, 0xcd, 0xd3, 0x4c, 0x7e, 0x02, 00, 00, 00, 31, 34, 0x2e, 0x36, 36, 36
            };

            var get = new Get<decimal>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            get.ReadExtras(expected);
            Assert.AreEqual(DataFormat.Json, get.Format);
        }

        [Test]
        public void Test_When_Type_Is_Int32()
        {
            var key = "intkey";
            var expected = new byte[]
            {
                81, 00, 00, 00, 04, 00, 00, 00, 00, 00, 00, 06, 00, 00, 00, 0x03, 00, 04, 0x0e, 0x9f, 43, 0xd8, 0xbd, 92, 02, 00, 00, 00, 31, 34
            };

            var get = new Get<int>(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new AutoByteConverter()));
            get.ReadExtras(expected);
            Assert.AreEqual(DataFormat.Json, get.Format);
        }

        [Test]
        public void Test_CreateExtras_When_Type_Is_Int()
        {
            var key = "intkey";
            var expected = new byte[]
            {
                0x02, 0x00, 0x00, 0x04
            };

            var value = 14;
            var set = new Set<int?>(key, value, GetVBucket(), new AutoByteConverter());
            var bytes = set.CreateExtras();

            Assert.AreEqual(expected[0], bytes[0]);
            Assert.AreEqual(DataFormat.Json, set.Format);
        }
    }
}
