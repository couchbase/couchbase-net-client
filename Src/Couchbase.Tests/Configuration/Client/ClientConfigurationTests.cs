using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Client
{
    [TestFixture]
    public class ClientConfigurationTests
    {
        [Test]
        public void Test_Default()
        {
            var config = new ClientConfiguration();
            Assert.AreEqual(1, config.BucketConfigs.Count);

            var bucketConfig = config.BucketConfigs.First().Value;

            IPAddress ipAddress;
            IPAddress.TryParse("127.0.0.1", out ipAddress);
            var endPoint = new IPEndPoint(ipAddress, bucketConfig.Port);
            Assert.AreEqual(endPoint, bucketConfig.GetEndPoint());

            Assert.IsEmpty(bucketConfig.Password);
            Assert.IsEmpty(bucketConfig.Username);
            Assert.AreEqual(11210, bucketConfig.Port);
            Assert.AreEqual("default", bucketConfig.BucketName);

            Assert.AreEqual(2, bucketConfig.PoolConfiguration.MaxSize);
            Assert.AreEqual(1, bucketConfig.PoolConfiguration.MinSize);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.RecieveTimeout);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.SendTimeout);
            Assert.AreEqual(10000, bucketConfig.PoolConfiguration.ShutdownTimeout);
        }

        [Test]
        public void Test_Custom()
        {
            var config = new ClientConfiguration
            {
                PoolConfiguration = new PoolConfiguration
                {
                    MaxSize = 10,
                    MinSize = 10
                }
            };
            config.Initialize();

            Assert.AreEqual(1, config.BucketConfigs.Count);

            var bucketConfig = config.BucketConfigs.First().Value;

            IPAddress ipAddress;
            IPAddress.TryParse("127.0.0.1", out ipAddress);
            var endPoint = new IPEndPoint(ipAddress, bucketConfig.Port);
            Assert.AreEqual(endPoint, bucketConfig.GetEndPoint());

            Assert.IsEmpty(bucketConfig.Password);
            Assert.IsEmpty(bucketConfig.Username);
            Assert.AreEqual(11210, bucketConfig.Port);
            Assert.AreEqual("default", bucketConfig.BucketName);

            Assert.AreEqual(10, bucketConfig.PoolConfiguration.MaxSize);
            Assert.AreEqual(10, bucketConfig.PoolConfiguration.MinSize);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.RecieveTimeout);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.SendTimeout);
            Assert.AreEqual(10000, bucketConfig.PoolConfiguration.ShutdownTimeout);
        }

        [Test]
        public void Test_CustomBucketConfigurations()
        {
            var config = new ClientConfiguration
            {
                BucketConfigs = new Dictionary<string, BucketConfiguration>
                {
                    {"default", new BucketConfiguration
                    {
                        PoolConfiguration = new PoolConfiguration
                        {
                            MaxSize = 5,
                            MinSize = 5
                        }
                    }},
                    {"authenticated", new BucketConfiguration
                    {
                        PoolConfiguration = new PoolConfiguration
                        {
                            MaxSize = 6,
                            MinSize = 4
                        },
                        Password = "password",
                        Username = "username",
                        BucketName = "authenticated"
                    }}
                }
            };

            config.Initialize();
            Assert.AreEqual(2, config.BucketConfigs.Count);

            var bucketConfig = config.BucketConfigs.First().Value;

            IPAddress ipAddress;
            IPAddress.TryParse("127.0.0.1", out ipAddress);
            var endPoint = new IPEndPoint(ipAddress, bucketConfig.Port);
            Assert.AreEqual(endPoint, bucketConfig.GetEndPoint());

            Assert.IsEmpty(bucketConfig.Password);
            Assert.IsEmpty(bucketConfig.Username);
            Assert.AreEqual(11210, bucketConfig.Port);
            Assert.AreEqual("default", bucketConfig.BucketName);

            Assert.AreEqual(5, bucketConfig.PoolConfiguration.MaxSize);
            Assert.AreEqual(5, bucketConfig.PoolConfiguration.MinSize);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.RecieveTimeout);
            Assert.AreEqual(2500, bucketConfig.PoolConfiguration.SendTimeout);
            Assert.AreEqual(10000, bucketConfig.PoolConfiguration.ShutdownTimeout);
        }
    }
}
