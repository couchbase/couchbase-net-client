using System;
using System.Collections.Generic;
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

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Test_That_UseSsl_Reflects_When_EnableCertificateAuthentication_Is_Set(bool enabled)
        {
            var config = new ClientConfiguration
            {
                EnableCertificateAuthentication = enabled
            };
            config.Initialize();
            Assert.AreEqual(enabled, config.PoolConfiguration.UseSsl);
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void Test_That_UseSsl_Reflects_When_EnableCertificateAuthentication_Is_Set_On_BucketConfiguration(
            bool enabled)
        {
            var config = new ClientConfiguration
            {
                EnableCertificateAuthentication = enabled
            };
            config.Initialize();

            Assert.AreEqual(enabled, config.BucketConfigs["default"].PoolConfiguration.UseSsl);
        }

        [Test]
        public void When_PoolConfiguration_UseSsl_Is_True_And_EnableCertificateAuthentication_Is_False_UseSsl_Is_Not_Overwritten()
        {
            var config = new ClientConfiguration
            {
                EnableCertificateAuthentication = false,
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "default", new BucketConfiguration
                        {
                            PoolConfiguration = new PoolConfiguration
                            {
                                UseSsl = true
                            }
                        }
                    }
                }
            };
            config.Initialize();

            Assert.IsTrue(config.BucketConfigs["default"].PoolConfiguration.UseSsl);
        }

        [Test]
        public void When_ClientConfiguration_UseSsl_Is_True_And_EnableCertificateAuthentication_Is_False_UseSsl_Is_Not_Overwritten()
        {
            var config = new ClientConfiguration
            {
                EnableCertificateAuthentication = false,
                UseSsl = true,
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {
                        "default", new BucketConfiguration
                        {
                        }
                    }
                }
            };
            config.Initialize();

            Assert.IsTrue(config.BucketConfigs["default"].PoolConfiguration.UseSsl);
        }
    }
}
