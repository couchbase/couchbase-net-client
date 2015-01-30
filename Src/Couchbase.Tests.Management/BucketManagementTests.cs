using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests.Management
{
    [TestFixture]
    public class BucketManagementTests
    {
        [Test]
        public void Test_InsertDesignDocument()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var manager = bucket.CreateManager("Administrator", "");
                    var designDoc = File.ReadAllText(@"Data\\DesignDocs\\by_field.json");
                    var result = manager.InsertDesignDocument("by_field", designDoc).Result;
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_UpdateDesignDocument()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var manager = bucket.CreateManager("Administrator", "");
                    var designDoc = File.ReadAllText(@"Data\\DesignDocs\\by_field2.json");
                    var result = manager.InsertDesignDocument("by_field", designDoc).Result;
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_GetDesignDocument()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var manager = bucket.CreateManager("Administrator", "");
                    var result = manager.GetDesignDocument("by_field").Result;
                    Assert.IsNotNull(result.Success);
                }
            }
        }

        [Test]
        public void Test_GetDesignDocuments()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var manager = bucket.CreateManager("Administrator", "");
                    var result = manager.GetDesignDocuments().Result;
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_RemoveDesignDocument()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var manager = bucket.CreateManager("Administrator", "");
                    var result = manager.RemoveDesignDocument("by_field").Result;
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_Flush()
        {
            var configuration = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            using (var cluster = new Cluster(configuration))
            {
                using (var bucket = cluster.OpenBucket())
                {
                    var manager = bucket.CreateManager("Administrator", "");
                    var result = manager.Flush().Result;
                    Assert.IsTrue(result.Success);
                }
            }
        }
    }
}
