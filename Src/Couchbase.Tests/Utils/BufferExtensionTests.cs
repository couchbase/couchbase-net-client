using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
{
    [TestFixture]
    public class BufferExtensionTests
    {
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
