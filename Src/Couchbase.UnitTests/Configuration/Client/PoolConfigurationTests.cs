using System;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration.Client
{
    [TestFixture]
    public class PoolConfigurationTests
    {
        [TestCase(0, 1, Description = "MaxSize is less than 1")]
        [TestCase(21, 1, Description = "MaxSize is greater than 20")]
        [TestCase(1, 0, Description = "MinSize is less than 1")]
        [TestCase(1, 21, Description = "MinSize is greater than 20")]
        [TestCase(5, 10, Description = "Maxsize is greater than MinSize")]
        public void THorws_Argument_Exception_If_MaxSize_Is_Not_Valid(int maxSize, int minSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoolConfiguration(maxSize, minSize));
        }
    }
}
