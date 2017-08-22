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
        [TestCase(1, -1, Description = "MinSize is less than 0")]
        [TestCase(501, 501, Description = "MinSize is greater than 500")]
        [TestCase(5, 10, Description = "Maxsize is greater than MinSize")]
        public void Throws_Argument_Exception_If_Connection_Values_Are_Not_Valid(int maxSize, int minSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PoolConfiguration
            {
                MaxSize = maxSize,
                MinSize = minSize
            }.Validate());
        }

        [Test]
        public void Can_Clone_PoolConfiguration()
        {
            var poolConfig = new PoolConfiguration
            {
                MaxSize = 10,
                MinSize = 5,
                BucketName = "default"
            };

            var clonedConfig = poolConfig.Clone(new Uri("http://test.com"));

            Assert.IsNotNull(clonedConfig);
            Assert.AreEqual(poolConfig.MaxSize, clonedConfig.MaxSize);
            Assert.AreEqual(poolConfig.MinSize, clonedConfig.MinSize);
            Assert.AreEqual(poolConfig.BucketName, clonedConfig.BucketName);
        }
    }
}
