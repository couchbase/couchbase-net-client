using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.CarrierPublication;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.Tests.Configuration.Server.Providers.CarrierPublication
{
    [TestFixture]
    internal class CarrierPublicationProviderTests : IConfigListener
    {
        [Test]
        public void Test_RegisterListener()
        {
            var configuration = new ClientConfiguration();
            var provider = new CarrierPublicationProvider(configuration);

            provider.RegisterListener(this);
        }

        public string Name
        {
            get { return "default"; }
        }

        public void NotifyConfigChanged(IConfigInfo configInfo)
        {
            throw new NotImplementedException();
        }

        public void NotifyConfigChanged(IConfigInfo configInfo, IConnectionPool connectionPool)
        {
            Assert.IsNotNull(configInfo);
            Assert.IsNotNull(connectionPool);
        }
    }
}
