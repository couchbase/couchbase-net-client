#if NET452

using System;
using System.Configuration;
using Couchbase.Configuration.Client.Providers;
using Couchbase.Configuration.Server.Providers;
using NUnit.Framework;

namespace Couchbase.UnitTests.Configuration.Client
{
    [TestFixture]
    public class CouchbaseClientSectionTests
    {
        [Test]
        public void DefaultConfig_AllConfigurationProviders()
        {
            var config =
                ConfigurationManager.GetSection("couchbaseClients/couchbase") as CouchbaseClientSection;
            Assert.NotNull(config);

            Assert.AreEqual(ServerConfigurationProviders.CarrierPublication | ServerConfigurationProviders.HttpStreaming, config.ConfigurationProviders);
        }

        [Test]
        public void HttpStreamingOnly_ReadsCorrectly()
        {
            var config =
                ConfigurationManager.GetSection("couchbaseClients/httpStreamingOnly") as CouchbaseClientSection;
            Assert.NotNull(config);

            Assert.AreEqual(ServerConfigurationProviders.HttpStreaming, config.ConfigurationProviders);
        }
    }
}

#endif