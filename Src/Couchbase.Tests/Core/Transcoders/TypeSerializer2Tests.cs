﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.Core.Transcoders
{
    [TestFixture]
    public class TypeTranscoder2Tests
    {
        [Test]
        public void Test_Serialize_Int16()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            Int16 data = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = data.GetTypeCode()
            };

            var expected = new byte[] {0x00, 0x05};
            var actual = transcoder.Encode(data, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_UInt16()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            UInt16 data = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = data.GetTypeCode()
            };

            var expected = new byte[] { 0x00, 0x05 };
            var actual = transcoder.Encode(data, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_Int32()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            Int32 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = data.GetTypeCode()
            };

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x09 };
            var actual = transcoder.Encode(data, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_UInt32()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            UInt32 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = data.GetTypeCode()
            };

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x09 };
            var actual = transcoder.Encode(data, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_Int64()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            Int64 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = data.GetTypeCode()
            };

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09 };
            var actual = transcoder.Encode(data, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_UInt64()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            UInt64 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = data.GetTypeCode()
            };

            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x09 };
            var actual = transcoder.Encode(data, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Serialize_String()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            string data = "Hello";

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = data.GetTypeCode()
            };

            var expected = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f };
            var actual = transcoder.Encode(data, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Null()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = TypeCode.Empty
            };

            var expected = new byte[0];
            var actual = transcoder.Encode<string>(null, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Char()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            var value = 'o';

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = value.GetTypeCode()
            };

            var expected = new byte[] { 0x6f };
            var actual = transcoder.Encode(value, flags);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Poco()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            var value = new Person {Name = "jeff"};

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Type.GetTypeCode(typeof(Person))
            };

            var bytes = transcoder.Encode(value, flags);
            var actual = transcoder.Decode<Person>(new ArraySegment<byte>(bytes), 0, bytes.Length, flags);

            Assert.AreEqual(value.Name, actual.Name);
        }

        [Test]
        public void Test_Deserialize_Int()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            var five = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = five.GetTypeCode()
            };

            var bytes = transcoder.Encode(five, flags);
            var actual = transcoder.Decode<int>(bytes, 0, bytes.Length, flags);
            Assert.AreEqual(five, actual);

        }

        [Test]
        public void Test_Deserialize_Null()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            object value = null;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = TypeCode.Empty
            };

            var bytes = transcoder.SerializeAsJson(value);
            var actual = transcoder.Decode<object>(bytes, 0, bytes.Length, flags);
            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Deserialize_String()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            var value = "astring";

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = value.GetTypeCode()
            };

            var bytes = transcoder.Encode(value, flags);
            var bytes1 = Encoding.UTF8.GetBytes(value);
            var actual = transcoder.Decode<string>(bytes, 0, bytes.Length, flags);
            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Byte_Array()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            var value = new byte[] {0x00, 0x00, 0x01};

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Binary,
                TypeCode = Type.GetTypeCode(typeof(byte[]))
            };

            var bytes = transcoder.Encode(value, flags);
            Assert.AreEqual(bytes, value);

            var actual = transcoder.Decode<byte[]>(bytes, 0, bytes.Length, flags);
            Assert.AreEqual(bytes, actual);
        }

        [Serializable]
        public class Person
        {
            public string Name { get; set; }
        }

        [Test]
        public void Test_Json_Deserialize_Int()
        {
            var transcoder = new DefaultTranscoder(new ManualByteConverter());
            int value = 42;

            var bytes = transcoder.SerializeAsJson(value);
            var actual = transcoder.DeserializeAsJson<int>(bytes, 0, bytes.Length);

            Assert.AreEqual(value, actual);
        }
    }
}
