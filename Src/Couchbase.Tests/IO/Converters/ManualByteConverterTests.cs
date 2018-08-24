﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Converters;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Converters
{
    [TestFixture]
    public sealed class ManualByteConverterTests
    {
        private byte[] _buffer;

        [SetUp]
        public void SetUp()
        {
            _buffer = new byte[]
            {
                0x80, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x00, 0x00, 0x00, 
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };
        }
        /* 
            Field        (offset) (value)
            Magic        (0)    : 0x80
            Opcode       (1)    : 0x00
            Key length   (2,3)  : 0x0005
            Extra length (4)    : 0x00
            Data type    (5)    : 0x00
            VBucket      (6,7)  : 0x0000
            Total body   (8-11) : 0x00000005
            Opaque       (12-15): 0x00000000
            CAS          (16-23): 0x0000000000000000
            Extras              : None
            Key          (24-29): The textual string: "Hello"
            Value               : None
         */

        [Test]
        public void Test_ToByte()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = converter.ToByte(_buffer, 0);
            const int expected = 128;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToInt16()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = converter.ToInt16(_buffer, 2);
            const int expected = 5;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToUInt16()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = converter.ToUInt16(_buffer, 2);
            const uint expected = 5u;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToInt32()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = converter.ToInt32(_buffer, 8);
            const uint expected = 5u;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToUInt32()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = converter.ToUInt32(_buffer, 8);
            const int expected = 5;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToString()
        {
            const int offset = 24;
            const int length = 5;

#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = converter.ToString(_buffer, offset, length);
            const string expected = "Hello";

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromString()
        {
            /*var converter = new ManualByteConverter();
            var actual = converter.FromString("Hello");
            var expected = new byte[]{0x48, 0x65, 0x6c, 0x6c, 0x6f};

            Assert.AreEqual(expected, actual);*/
        }

        [Test]
        public void Test_FromInt16()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = new byte[8];
            converter.FromInt16(5, actual, 2);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromUInt16()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = new byte[8];
            converter.FromUInt16(5, actual, 2);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromInt32()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = new byte[11];
            converter.FromInt32(5, actual, 3);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromUInt32()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = new byte[11];
            converter.FromUInt32(5, actual, 3);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromInt64()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = new byte[15];
            converter.FromInt64(5, actual, 3);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00,0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromUInt64()
        {
#pragma warning disable 618
            var converter = new ManualByteConverter();
#pragma warning restore 618
            var actual = new byte[15];
            converter.FromUInt64(5, actual, 3);
            var expected = new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test()
        {
            var bytes = new byte[] {0x00, 0x00, 0x00, 0x01};
            var expected = 1;

            var actual1 = BitConverter.ToInt32(bytes, 0);
            Assert.AreNotEqual(expected, actual1);

            var array = new byte[4];
            Buffer.BlockCopy(bytes, 0, array, 0, 4);
            Array.Reverse(array);

            var actual2 = BitConverter.ToInt32(array, 0);
            Assert.AreEqual(expected, actual2);
        }
    }
}
