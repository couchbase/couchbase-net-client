using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
