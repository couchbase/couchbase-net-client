using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client.Providers;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Client
{
    [TestFixture]
    public class ClientClientSectionTests
    {
        [Test]
        public void When_GetSection_Called_Section_Is_Returned()
        {
            var section = ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.IsNotNull(section);
        }

        [Test]
        public void When_GetSection_Called_CouchbaseClientSection_Is_Returned()
        {
            var section = ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.IsNotNull(section as CouchbaseClientSection);
        }

        [Test]
        public void Test_That_CouchbaseClientSection_Has_Localhost_Uri()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            var servers = new UriElement[section.Servers.Count];
// ReSharper disable once CoVariantArrayConversion
            section.Servers.CopyTo(servers, 0);
            Assert.AreEqual("http://localhost:8091", servers[0].Uri.OriginalString);
        }

        [Test]
        public void Test_That_CouchbaseClientSection_Has_At_Least_One_Element()
        {
            var section = (CouchbaseClientSection) ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.Greater(section.Servers.Count, 0);
        }

        [Test]
        public void When_UseSsl_Is_True_In_AppConfig_UseSsl_Returns_True()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.IsTrue(section.UseSsl);
        }

        [Test]
        public void When_No_Bucket_Is_Defined_Default_Bucket_Is_Used()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.Greater(section.Buckets.Count, 0);

            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);
            Assert.AreEqual("default", buckets[0].Name);
        }

        [Test]
        public void When_Bucket_Is_Defined_That_Bucket_Is_Used()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1");
            Assert.Greater(section.Buckets.Count, 0);

            var buckets = new BucketElement[section.Buckets.Count];
            section.Buckets.CopyTo(buckets, 0);

            var bucket = buckets.First();
            Assert.AreEqual("testbucket", bucket.Name);
            Assert.AreEqual("shhh!", bucket.Password);
            Assert.IsFalse(bucket.UseSsl);
        }

        [Test]
        public void Test_Default_Ports()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase");
            Assert.AreEqual(11207, section.SslPort);
            Assert.AreEqual(8091, section.MgmtPort);
            Assert.AreEqual(8092, section.ApiPort);
            Assert.AreEqual(18091, section.HttpsMgmtPort);
            Assert.AreEqual(18092, section.HttpsApiPort);
            Assert.AreEqual(11210, section.DirectPort);
        }

        [Test]
        public void Test_Custom_Ports()
        {
            var section = (CouchbaseClientSection)ConfigurationManager.GetSection("couchbaseClients/couchbase_1");
            Assert.AreEqual(443, section.SslPort);
            Assert.AreEqual(8095, section.MgmtPort);
            Assert.AreEqual(8094, section.ApiPort);
            Assert.AreEqual(18099, section.HttpsMgmtPort);
            Assert.AreEqual(18098, section.HttpsApiPort);
            Assert.AreEqual(11219, section.DirectPort);
        }
    }
}
