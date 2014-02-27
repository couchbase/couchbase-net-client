using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Serialization
{
    [TestFixture]
    public class BucketTests
    {
        private BucketConfig _bucket1;
        private BucketConfig _bucket2;
        private BucketConfig _bucket3;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _bucket1 = new BucketConfig
            {
                Name = "default",
                AuthType = "SASL",
                NodeLocator = "vbucket",
                BucketType = "membase",
                SaslPassword = "",
                Uuid = "e3f109232cba04155b3182fd42cd1e83",
                VBucketServerMap = new VBucketServerMap
                {
                    HashAlgorithm = "CRC",
                    NumReplicas = 1,
                    ServerList = new[] {"192.168.56.101:11210", "192.168.56.104:11210"},
                    VBucketMap = new[] {new[] {1, 0}, new[] {1, 0}, new[] {1, 0}}
                },
                Nodes = new[]
                {
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.101:11210",
                        Status = "healthy"
                    },
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.104:11210",
                        Status = "healthy"
                    }
                }
            };

            _bucket2 = new BucketConfig
            {
                Name = "default",
                AuthType = "SASL",
                NodeLocator = "vbucket",
                BucketType = "membase",
                SaslPassword = "",
                Uuid = "e3f109232cba04155b3182fd42cd1e83",
                VBucketServerMap = new VBucketServerMap
                {
                    HashAlgorithm = "CRC",
                    NumReplicas = 1,
                    ServerList = new[] { "192.168.56.101:11210", "192.168.56.104:11210" },
                    VBucketMap = new[] { new[] { 1, 0 }, new[] { 1, 0 }, new[] { 1, 0 } }
                },
                Nodes = new[]
                {
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.101:11210",
                        Status = "healthy"
                    },
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.104:11210",
                        Status = "healthy"
                    }
                }
            };

            _bucket3 = new BucketConfig
            {
                Name = "test",
                AuthType = "SASL",
                NodeLocator = "vbucket",
                BucketType = "membase",
                SaslPassword = "",
                Uuid = "e3f109232cba04155b3182fd42cd1e83",
                VBucketServerMap = new VBucketServerMap
                {
                    HashAlgorithm = "CRC",
                    NumReplicas = 1,
                    ServerList = new[] {"192.168.56.101:11210", "192.168.56.104:11210"},
                    VBucketMap = new[] {new[] {1, 0}, new[] {1, 0}, new[] {1, 0}}
                },
                Nodes = new[]
                {
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.101:11210",
                        Status = "healthy"
                    },
                    new Node
                    {
                        ClusterMembership = "active",
                        Hostname = "192.168.56.104:11210",
                        Status = "healthy"
                    }
                }
            };
        }

        [Test]
        public void Test_GetHashcode()
        {
            Assert.AreEqual(_bucket1.GetHashCode(), _bucket2.GetHashCode());
            Assert.AreNotEqual(_bucket2.GetHashCode(), _bucket3.GetHashCode());
        }

        [Test]
        public void Test_Equals()
        {
            Assert.IsTrue(_bucket1.Equals(_bucket2));
            Assert.IsFalse(_bucket2.Equals(_bucket3));
        }
    }
}
