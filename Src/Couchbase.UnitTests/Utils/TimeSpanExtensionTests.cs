using System;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.UnitTests.Utils
{
    [TestFixture]
    public class TimeSpanExtensionTests
    {
        [Test]
        public void When_Days_GreaterThan24_Ttl_Is_Postive()
        {
            var twentyFourDays = new TimeSpan(24, 0, 0, 0);
            var twentyFiveDays = new TimeSpan(25, 0, 0, 0);

            var result1 = TimeSpanExtensions.ToTtl((uint) twentyFourDays.TotalMilliseconds);
            var result2 = TimeSpanExtensions.ToTtl((uint)twentyFiveDays.TotalMilliseconds);

            Assert.Positive(result1);
            Assert.Positive(result2);
        }

        [TestCase("100", (long) 100)]
        [TestCase("100us", (long) 100)]
        [TestCase("100ms", (long) 100000)]
        [TestCase("100s", (long) 100000000)]
        [TestCase(null, null)]
        [TestCase("", null)]
        [TestCase("jndlnfls", null)]
        public void Test_TryConverToMicros(string duration, long? expected)
        {
            var result = TimeSpanExtensions.TryConvertToMicros(duration, out var value);
            if (expected.HasValue)
            {
                Assert.IsTrue(result);
                Assert.AreEqual(expected, value);
            }
            else
            {
                Assert.IsFalse(result);
            }
        }

        [Test]
        [TestCase(0u, 0u)]
        [TestCase(999u, 1u)]
        [TestCase(1001u, 1u)]
        [TestCase(2500u, 2u)]
        public void Test_ToTtl(uint expiry, uint expected)
        {
            var doc = new Document<dynamic>
            {
                Expiry = expiry
            };

            var actual = doc.Expiry.ToTtl();

            Assert.AreEqual(expected, actual);
        }

        [Test]
        [TestCase(0u, 0u)]
        [TestCase(999u, 1u)]
        [TestCase(1001u, 1u)]
        [TestCase(2500u, 2u)]
        public void Test_ToTtl_TimeStamp(uint expiry, uint expected)
        {
            var actual = TimeSpan.FromMilliseconds(expiry).ToTtl();

            Assert.AreEqual(expected, actual);
        }
    }
}
