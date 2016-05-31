using System;
using System.Linq;
using System.Text;
using Couchbase.IO.Converters;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Converters
{
    [TestFixture]
    public sealed class DefaultConverterTests
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
        public void Test_ToBoolean()
        {
            var converter = new DefaultConverter();
            bool theBool = true;
            var bytes =  BitConverter.GetBytes(theBool);
            var actual = converter.ToBoolean(bytes, 0, false);
            const bool expected = true;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToBoolean_UseNbo()
        {
            var converter = new DefaultConverter();
            bool theBool = true;
            var bytes = BitConverter.GetBytes(theBool).Reverse().ToArray();
            var actual = converter.ToBoolean(bytes, 0, true);
            const bool expected = true;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToDouble_UseNbo()
        {
            var converter = new DefaultConverter();
            double theDouble = 2.3d;

            var bytes = BitConverter.GetBytes(theDouble).Reverse().ToArray();
            var actual = converter.ToDouble(bytes, 0, true);
            const double expected = 2.3d;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToDouble()
        {
            var converter = new DefaultConverter();
            double theDouble = 2.3d;

            var bytes = BitConverter.GetBytes(theDouble);
            var actual = converter.ToDouble(bytes, 0, false);
            const double expected = 2.3d;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToSingle_UseNbo()
        {
            var converter = new DefaultConverter();
            float theDouble = 2.3f;

            var bytes = BitConverter.GetBytes(theDouble).Reverse().ToArray();
            var actual = converter.ToSingle(bytes, 0, true);
            const float expected = 2.3f;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToSingle()
        {
            var converter = new DefaultConverter();
            float theSingle = 2.3f;

            var bytes = BitConverter.GetBytes(theSingle);
            var actual = converter.ToSingle(bytes, 0, false);
            const float expected = 2.3f;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToDateTime()
        {
            var converter = new DefaultConverter();
            var theDateTime = new DateTime(1972, 12, 7);

            var bytes = BitConverter.GetBytes(theDateTime.ToBinary());
            var actual = converter.ToDateTime(bytes, 0, false);
            var expected = new DateTime(1972, 12, 7);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToDateTime_UseNbo()
        {
            var converter = new DefaultConverter();
            var theDateTime = new DateTime(1972, 12, 7);

            var bytes = BitConverter.GetBytes(theDateTime.ToBinary()).Reverse().ToArray();
            var actual = converter.ToDateTime(bytes, 0, true);
            var expected = new DateTime(1972, 12, 7);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToByte()
        {
            var converter = new DefaultConverter();
            var actual = converter.ToByte(_buffer, 0);
            const int expected = 128;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToInt16()
        {
            var converter = new DefaultConverter();

            var actual = converter.ToInt16(_buffer, 2);
            const int expected = 5;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToUInt16()
        {
            var converter = new DefaultConverter();
            var actual = converter.ToUInt16(_buffer, 2);
            const uint expected = 5u;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToInt32()
        {
            var converter = new DefaultConverter();
            var actual = converter.ToInt32(_buffer, 8);
            const uint expected = 5u;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToUInt32()
        {
            var converter = new DefaultConverter();
            var actual = converter.ToUInt32(_buffer, 8);
            const int expected = 5;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_ToString()
        {
            const int offset = 24;
            const int length = 5;

            var converter = new DefaultConverter();
            var actual = converter.ToString(_buffer, offset, length);
            const string expected = "Hello";

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromString()
        {
#pragma warning disable 618
            var converter = new DefaultConverter();
#pragma warning restore 618
            var buffer = new byte[Encoding.UTF8.GetByteCount("Hello")];
            converter.FromString("Hello", buffer, 0);
            var expected = new byte[]{0x48, 0x65, 0x6c, 0x6c, 0x6f};

            Assert.AreEqual(expected, buffer);
        }

        [Test]
        public void Test_FromInt16()
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromInt16(5, actual, 2);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromUInt16()
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromUInt16(5, actual, 2);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromInt32()
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromInt32(5, actual, 3);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromUInt32()
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromUInt32(5, actual, 3);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromInt64()
        {
            var converter = new DefaultConverter();
            var actual = new byte[15];
            converter.FromInt64(5, actual, 3);
            var expected = new byte[] { 0x00, 0x00, 0x00, 0x00,0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_FromUInt64()
        {
            var converter = new DefaultConverter();
            var actual = new byte[15];
            converter.FromUInt64(5, actual, 3);
            var expected = new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00 };

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void Test_Cas()
        {
            var converter = new DefaultConverter();
            var bytes = new byte[] {255, 255, 255, 255, 229, 93, 159, 223};
            const ulong expected = 18446744073262702559;
            var actual = converter.ToUInt64(bytes, 0);
            Assert.AreEqual(expected, actual);
        }

         [Test]
        public void Test_Cas2()
        {
            var converter = new DefaultConverter();
             var bytes = new byte[]
             {
                 0x00,
		        0x00,
		        0xa9,
		        0x53,
		        0x5f,
		        0x3d,
		        0xa7,
		        0x0f
             };
             const ulong expected = 186175545255695;
            var actual = converter.ToUInt64(bytes, 0);
            Assert.AreEqual(expected, actual);
        }
    }
}

