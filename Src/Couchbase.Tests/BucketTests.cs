using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class BucketTests
    {
        [Test]
        public void Test_Insert()
        {
            var config = new ClientConfiguration()
            {
                ProviderConfigs = new List<ProviderConfiguration>
                {
                    new ProviderConfiguration
                    {
                        Name = "CarrierPublication",
                        TypeName =
                            "Couchbase.Configuration.Server.Providers.CarrierPublication.CarrierPublicationProvider, Couchbase"
                    }
                }
            };
            var cluster = new Cluster(config);
            using (var bucket = cluster.OpenBucket("default"))
            {
                var result = bucket.Insert("fookey", "fookeyvalue");
                Assert.IsTrue(result.Success);
            }
        }
    }
}
