using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Cryptography;
using NUnit.Framework;

namespace Couchbase.Tests.Cryptography
{
    [TestFixture]
    public class CrC32Tests
    {
        [Test]
        public void Test_ComputeHash()
        {
            const string key = "XXXXX";
            const int expected = 13701;
            var crc = new Crc32();
            var bytes = Encoding.UTF8.GetBytes(key);
            var actual = BitConverter.ToUInt32(crc.ComputeHash(bytes), 0);

            Assert.AreEqual(expected, actual);
        }
    }
}
