using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class TimeSpanExtensionsTests
    {
        [Fact]
        //Fixes the minimal amount of expiry supported by the server
        public void When_Less_Than_1000MS_Returns_OneSecond()
        {
            var lifespan = TimeSpan.FromMilliseconds(999).ToTtl();
            Assert.Equal(1u, lifespan);
        }

        [Fact]
        public void When_Equal_To_1000MS_Returns_OneSecond()
        {
            var lifespan = TimeSpan.FromMilliseconds(1000).ToTtl();
            Assert.Equal(1u, lifespan);
        }

        [Fact]
        public void When_Negative_Value_Returns_Zero()
        {
            var lifespan = TimeSpan.FromMilliseconds(-1).ToTtl();
            Assert.Equal(0u, lifespan);
        }
    }
}
