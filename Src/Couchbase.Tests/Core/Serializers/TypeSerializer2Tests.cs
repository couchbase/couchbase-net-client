using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serializers;
using Couchbase.IO;
using Couchbase.IO.Converters;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Serializers
{
    [TestFixture]
    public class TypeSerializer2Tests
    {
        [Test]
        public void Test_Serialize_Int16()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            Int16 data = 5;

            var expected = new byte[] {0x00, 0x05};
            var actual = serializer.Serialize(data);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_UInt16()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            UInt16 data = 5;

            var expected = new byte[] { 0x00, 0x05 };
            var actual = serializer.Serialize(data);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_Int32()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            Int32 data = 9;

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x09 };
            var actual = serializer.Serialize(data);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_UInt32()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            UInt32 data = 9;

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x09 };
            var actual = serializer.Serialize(data);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_Int64()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            Int64 data = 9;

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09 };
            var actual = serializer.Serialize(data);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_UInt64()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            UInt64 data = 9;

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09 };
            var actual = serializer.Serialize(data);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_String()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            string data = "Hello";

            var expected = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f };
            var actual = serializer.Serialize(data);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Null()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());

            var expected = new byte[0];
            var actual = serializer.Serialize<string>(null);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Char()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            var value = 'o';
            var expected = new byte[] { 0x6f };
            var actual = serializer.Serialize(value);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Poco()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            var value = new Person {Name = "jeff"};
            var bytes = serializer.Serialize(value);

            var actual = serializer.Deserialize<Person>(new ArraySegment<byte>(bytes), 0, bytes.Length);

            Assert.AreEqual(value.Name, actual.Name);
        }

        [Test]
        public void Test_Deserialize_Int()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            var five = 5;
            var bytes = serializer.Serialize(five);
            var actual = serializer.Deserialize<int>(bytes, 0, bytes.Length);
            Assert.AreEqual(five, actual);

        }

        [Test]
        public void Test_Deserialize_Null()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            object value = null;
            var bytes = serializer.SerializeAsJson(value);
            var actual = serializer.Deserialize<object>(bytes, 0, bytes.Length);
            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Deserialize_String()
        {
            var serializer = new TypeSerializer2(new ManualByteConverter());
            var value = "astring";
            var bytes = serializer.Serialize(value);
            var bytes1 = Encoding.UTF8.GetBytes(value);
            var actual = serializer.Deserialize<string>(bytes, 0, bytes.Length);
            Assert.AreEqual(value, actual);
        }

        [Serializable]
        public class Person
        {
            public string Name { get; set; }
        }
    }
}
