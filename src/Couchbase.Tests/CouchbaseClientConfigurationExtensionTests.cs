using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using Couchbase.Configuration;
using Couchbase.Extensions;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class CouchbaseClientConfigurationExtensionTests
    {
        [Test]
        public void Test_DisplayConfig()
        {
            const string sectionName = "couchbase";
            var config = (ICouchbaseClientConfiguration) ConfigurationManager.GetSection(sectionName);
            var configString = config.GetConfig();
            Assert.IsNotNull(configString);
        }
    }
}
