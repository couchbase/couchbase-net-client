using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wintellect;

namespace Couchbase.Tests.Utils
{
    [TestFixture]
    public class BufferExtensionTests
    {
        private byte[] _buffer;

        [SetUp]
        public void SetUp()
        {
            /* 		
*/
            _buffer = new byte[]
            {
                0x81, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x13, 0x00,
                0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x44, 0x61,
                0x74, 0x61, 0x20, 0x65, 0x78, 0x69, 0x73, 0x74, 0x73, 0x20, 0x66, 0x6f, 0x72,
                0x20, 0x6b, 0x65, 0x79, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };
        }

        /*  Field        (offset) (value)
            Magic        (0)    : 0x81
            Opcode       (1)    : 0x02
            Key length   (2,3)  : 0x0000
            Extra length (4)    : 0x00
            Data type    (5)    : 0x00
            Status       (6,7)  : 0x0000
            Total body   (8-11) : 0x00000000
            Opaque       (12-15): 0x00000000
            CAS          (16-23): 0x0000000000000001
            Extras              : None
            Key                 : None
            Value               : None*/

        [Test]
        public void When_Buffer_Is_Zero_GetLengthSafe_Returns_Zero()
        {
            var buffer = new byte[0];
            var length = buffer.GetLengthSafe();
            const int expected = 0;
            Assert.AreEqual(expected, length);
        }

        [Test]
        public void When_Buffer_Is_Null_GetLengthSafe_Returns_Zero()
        {
            byte[] buffer = null;
            var length = buffer.GetLengthSafe();
            const int expected = 0;
            Assert.AreEqual(expected, length);
        }

        [Test]
        public void When_Buffer_Is_8bytes_GetLengthSafe_Returns_8()
        {
            var buffer = new byte[8];
            var length = buffer.GetLengthSafe();
            const int expected = 8;
            Assert.AreEqual(expected, length);
        }

        [Test]
        public void Test_Magic()
        {
            var value = _buffer[HeaderIndexFor.Magic];
            Assert.AreEqual(129, value);
        }

        [Test]
        public void Test_OpCode()
        {
            var value = _buffer[HeaderIndexFor.Opcode].ToOpCode();
            Assert.AreEqual(OperationCode.Add, value);
        }

        [Test]
        public void Test_KeyLength()
        {
            var value = BitConverter.ToInt16(_buffer, HeaderIndexFor.KeyLength);
            Assert.AreEqual(0, value);
        }

        [Test]
        public void Test_ExtrasLength()
        {
            var value = _buffer[HeaderIndexFor.ExtrasLength];
            Assert.AreEqual(0, value);
        }

        [Test]
        public void Test_DataType()
        {
            var value = _buffer[HeaderIndexFor.Datatype];
            Assert.AreEqual(0, value);
        }

        [Test]
        public void Test_Status()
        {
            var array = new byte[2];
            Buffer.BlockCopy(_buffer, HeaderIndexFor.Status, array, 0, 2);
            Array.Reverse(array);

            var value = (ResponseStatus)BitConverter.ToUInt16(array, 0);
            Assert.AreEqual(ResponseStatus.KeyExists, value);
        }

        [Test]
        public void Test_TotalBody()
        {
            var array = new byte[4];
            Buffer.BlockCopy(_buffer, HeaderIndexFor.Body, array, 0, 4);
            Array.Reverse(array);

            var value = BitConverter.ToUInt32(array, 0);
            Assert.AreEqual(19, value);
        }

        [Test]
        public void Test_Opaque()
        {
            var array = new byte[4];
            Buffer.BlockCopy(_buffer, HeaderIndexFor.Opaque, array, 0, 4);
            Array.Reverse(array);

            var value = BitConverter.ToUInt32(array, 0);
            Assert.AreEqual(value, 1);
        }

        [Test]
        public void Test_CAS()
        {
            var array = new byte[4];
            Buffer.BlockCopy(_buffer, HeaderIndexFor.Cas, array, 0, 4);
            Array.Reverse(array);

            var value = BitConverter.ToUInt32(array, 0);
            Assert.GreaterOrEqual(value, 0);
        }

        [Test]
        public void When_Array_Is_Null_GetLengthSafe_Returns_Zero()
        {
            byte[] buffer = null;
            const int expected = 0;
      
            var actual = buffer.GetLengthSafe();
            Assert.AreEqual(expected, actual);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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