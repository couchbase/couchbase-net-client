using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Utils;
using Moq;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class TouchOperationTests : OperationTestBase
    {
        [Test]
        public void When_Key_Exists_Touch_Returns_Success()
        {
            var key = "When_Key_Exists_Touch_Returns_Success";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            IOStrategy.Execute(delete);

            //Add the key
            var add = new Add<dynamic>(key, new { foo = "foo" }, GetVBucket(), new AutoByteConverter(), new DefaultTranscoder(new ManualByteConverter()), OperationLifespanTimeout);
            Assert.IsTrue(IOStrategy.Execute(add).Success);

            var touch = new Touch(key, GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()), OperationLifespanTimeout)
            {
                Expires = new TimeSpan(0, 0, 0, 3).ToTtl()
            };

            var result = IOStrategy.Execute(touch);
            Console.WriteLine(result.Message);
            Assert.IsTrue(result.Success);
        }

        /// <summary>
        /// Ensures that the memcached request packet returned by Write() matches an equivalent memcached packet
        /// while ignoring any bytes that are message instance specific...for example it ignores opaque. The
        /// test ensures that magic, opcode, key length, extras length, datatype, expiration and key are equal.
        /// </summary>
        [Test]
        public void Test_That_Write_Returns_Correct_Values_Ignoring_CAS_And_Opaque()
        {
            //A memcached request packet for touch command to compare against
            var buffer = new byte[] { 128, 28, 0, 9, 4, 0, 1, 26, 0, 0, 0, 13, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 1,
                109, 121, 107, 101, 121, 50, 50, 50, 50 };

            //create a touch operations
            var touch = new Touch("mykey2222", GetVBucket(), new AutoByteConverter(),
                new DefaultTranscoder(new AutoByteConverter()), OperationLifespanTimeout)
            {
                Expires = new TimeSpan(0, 0, 0, 1).ToTtl()
            };

            //get the request packet the operation will generate
            var actual = touch.Write();

            //test the byte fields which should be equivalent
            Assert.AreEqual(buffer.Take(6), actual.Take(6)); //magic, opcode, key lngth, extra lngth and datatype
            Assert.AreEqual(buffer.Skip(23).Take(4), actual.Skip(23).Take(4)); //expires value
            Assert.AreEqual(buffer.Skip(27).Take(9), actual.Skip(27).Take(9)); //key
        }
    }
}
