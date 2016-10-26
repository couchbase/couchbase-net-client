using System;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration.Client
{
    [TestFixture]
    public class PoolConfigurationTests
    {
        [TestCase(0, 1, Description = "MaxSize is less than 1")]
        [TestCase(501, 1, Description = "MaxSize is greater than 500")]
        [TestCase(1, 0, Description = "MinSize is less than 1")]
        [TestCase(501, 501, Description = "MinSize is greater than 500")]
        [TestCase(5, 10, Description = "Maxsize is greater than MinSize")]
        public void Throws_Argument_Exception_If_Connection_Values_Are_Not_Valid(int maxSize, int minSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoolConfiguration(maxSize, minSize));
        }
    }
}
