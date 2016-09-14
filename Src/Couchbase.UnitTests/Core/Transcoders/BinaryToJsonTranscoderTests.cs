using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using NUnit.Framework;
using Encoding = System.Text.Encoding;

namespace Couchbase.UnitTests.Core.Transcoders
{
    [TestFixture]
    public class BinaryToJsonTranscoderTests
    {
        byte[] GetBytes(object obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        [Test]
        public void Test_Decode_Poco()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            var value = new Person { Name = "jeff" };
            var bytes = GetBytes(value);

            var actual = transcoder.Decode<Person>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value.Name, actual.Name);
        }

        [Test]
        public void Test_Decode_Int()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            var five = 5;
            var bytes = BitConverter.GetBytes(five);

            var actual = transcoder.Decode<int>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(five, actual);

        }

        [Test]
        public void Test_Decode_EmptyByteArray()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            object value = new byte[0];
            var bytes = GetBytes(value);

            var actual = transcoder.Decode<object>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_String()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            var value = "astring";
            var bytes = Encoding.UTF8.GetBytes(value);

            var actual = transcoder.Decode<string>(bytes, 0, bytes.Length,OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_Char()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            var value = 'o';
            var bytes = Encoding.UTF8.GetBytes(new[] {value});

            var actual = transcoder.Decode<char>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_ByteArray()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            var value = new byte[] { 0x00, 0x00, 0x01 };
            var bytes = GetBytes(value);

            var actual = transcoder.Decode<byte[]>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_Short()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            short value = 1;
            var bytes = BitConverter.GetBytes(value);

            var actual = transcoder.Decode<short>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_Double()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            double value = 1.2;
            var bytes = BitConverter.GetBytes(value);

            var actual = transcoder.Decode<double>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_DateTime()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            DateTime value = DateTime.Now;
            var bytes = BitConverter.GetBytes(value.Ticks);

            var actual = transcoder.Decode<DateTime>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_Boolean()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            bool value = true;
            var bytes = BitConverter.GetBytes(value);

            var actual = transcoder.Decode<bool>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Test]
        public void Test_Decode_Byte()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            byte value = 0x0;
            var bytes = new[] {value};

            //sbyte and byte are not-supported by 1.X SDK
            Assert.Throws<ArgumentException>(()=>transcoder.Decode<byte>(bytes, 0, bytes.Length, OperationCode.Get));
        }

        [Test]
        public void Test_Decode_UInt16()
        {
            var transcoder = new BinaryToJsonTranscoder(new DefaultConverter());
            Int16 value = 1;
            var bytes = BitConverter.GetBytes(value);

            var actual = transcoder.Decode<Int16>(bytes, 0, bytes.Length, OperationCode.Get);

            Assert.AreEqual(value, actual);
        }

        [Serializable]
        public class Person
        {
            public string Name { get; set; }
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
