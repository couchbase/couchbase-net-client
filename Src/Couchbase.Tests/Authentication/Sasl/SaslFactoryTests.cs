using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.EAP;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class SaslFactoryTests
    {
        private DefaultIOStrategy _ioStrategy;
        private IConnectionPool<EapConnection> _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration { UseSsl = false };
            _connectionPool = new ConnectionPool<EapConnection>(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_GetFactory()
        {
            var factory = SaslFactory.GetFactory3();
            Assert.IsNotNull(factory);
        }

        [Test]
        public void When_PlainText_Provided_Factory_Returns_CramMd5Mechanism()
        {
            var factory = SaslFactory.GetFactory3();
            var mechanism = factory("authenticated", "secret", _ioStrategy, new ManualByteConverter());
            Assert.IsTrue(mechanism is CramMd5Mechanism);
        }
    }
}
