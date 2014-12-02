using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Couchbase.Utils;
using Couchbase.Core;

namespace Couchbase.Tests.Utils
{
    class TimeSpanExtensionsTests
    {
        [Test]
        public void When_Given_TimeSpan_Should_Convert_To_Seconds()
        {
            Assert.AreEqual(1, TimeSpan.FromMilliseconds(1800).ToTtl());
            Assert.AreEqual(3, TimeSpan.FromMinutes(0.05).ToTtl());
            Assert.AreEqual(1234, TimeSpan.FromSeconds(1234).ToTtl());
        }
    }
}
