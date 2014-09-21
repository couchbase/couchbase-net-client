using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests.Management
{
    [TestFixture]
    public class ClusterManagerTests
    {
        [Test]
        public void Test_AddNode()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var result = clusterManager.AddNode("192.168.56.103");
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void Test_RemoveNode()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var result = clusterManager.RemoveNode("192.168.56.103");
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void Test_FailoverNode()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var result = clusterManager.FailoverNode("192.168.56.103");
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void Test_ListBuckets()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var results = clusterManager.ListBuckets();
                Assert.Greater(results.Value.Count, 0);
            }
        }

        [Test]
        public void Test_Rebalance()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var result = clusterManager.Rebalance();
                Assert.IsTrue(result.Success);
            }
        }

        [Test]
        public void Test_ClusterInfo()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var result = clusterManager.ClusterInfo();
                Assert.NotNull(result.Success);
            }
        }

        [Test]
        public void Test_CreateBucket()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var result = clusterManager.CreateBucket("test");
                Assert.NotNull(result.Success);
            }
        }

        [Test]
        public void Test_RemoveBucket()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri("http://192.168.56.101:8091/pools")
                }
            };
            using (var cluster = new CouchbaseCluster(configuration))
            {
                var clusterManager = cluster.CreateManager("Administrator", "password");
                var result = clusterManager.RemoveBucket("test");
                Assert.IsTrue(result.Success);
            }
        }
    }
}
