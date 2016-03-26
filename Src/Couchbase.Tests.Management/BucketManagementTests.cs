using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests.Management
{
    [TestFixture]
    [Category("Integration")]
    public class BucketManagementTests
    {
        private const string DATA_PATH = "..\\..\\..\\Couchbase.Tests\\Data";
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
                    var designDoc = File.ReadAllText(DATA_PATH + @"\\DesignDocs\\by_field.json");
                    var result = manager.InsertDesignDocument("by_field", designDoc);
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_InsertDesignDocumentAsync()
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
                    var designDoc = File.ReadAllText(DATA_PATH+@"\\DesignDocs\\by_field.json");
                    var result = manager.InsertDesignDocumentAsync("by_field", designDoc).Result;
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
                    var designDoc = File.ReadAllText(DATA_PATH+@"\\DesignDocs\\by_field2.json");
                    var result = manager.InsertDesignDocument("by_field", designDoc);
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_UpdateDesignDocumentAsync()
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
                    var designDoc = File.ReadAllText(DATA_PATH+@"\\DesignDocs\\by_field2.json");
                    var result = manager.InsertDesignDocumentAsync("by_field", designDoc).Result;
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
                    var result = manager.GetDesignDocument("by_field");
                    Assert.IsNotNull(result.Success);
                }
            }
        }

        [Test]
        public void Test_GetDesignDocumentAsync()
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
                    var result = manager.GetDesignDocumentAsync("by_field").Result;
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
                    var result = manager.GetDesignDocuments();
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_GetDesignDocumentsAsync()
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
                    var result = manager.GetDesignDocumentsAsync().Result;
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
                    var result = manager.RemoveDesignDocumentAsync("by_field").Result;
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
                    var result = manager.Flush();
                    Console.WriteLine(result.Message);
                    Assert.IsTrue(result.Success);
                }
            }
        }

        [Test]
        public void Test_FlushAsync()
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
                    var result = manager.FlushAsync().Result;
                    Assert.IsTrue(result.Success);
                }
            }
        }

#region Indexing API

        [Test]
        public void BuildDeferredIndexes_WhenSucceed_Returns_Success()
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
                using (var bucket = cluster.OpenBucket("beer-sample"))
                {
                    var manager = bucket.CreateManager("Administrator", "");
                    Assert.IsTrue(manager.DropIndex("index1").Success);
                    Assert.IsTrue(manager.DropIndex("index2").Success);

                    Assert.IsTrue(manager.CreateIndex("index1", true, "name", "id").Success);
                    Assert.IsTrue(manager.CreateIndex("index2", true, "name", "id").Success);

                    var indexes = manager.ListIndexes();
                    Assert.AreEqual("deferred", indexes.First(x => x.Name == "index1").State);
                    Assert.AreEqual("deferred", indexes.First(x => x.Name == "index2").State);

                    var buildResults = manager.BuildDeferredIndexes();
                    Assert.IsTrue(buildResults.All(x=>x.Success));

                    manager.ListIndexes().ToList().ForEach(Console.WriteLine);

                    Assert.AreEqual("online", indexes.First(x => x.Name == "index1").State);
                    Assert.AreEqual("online", indexes.First(x => x.Name == "index2").State);
                }
            }
        }
#endregion
    }
}
