using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Strategies;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class SaslFactoryTests
    {
        private DefaultIOStrategy _ioStrategy;
        private IConnectionPool<Connection> _connectionPool;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(_address);
            var connectionPoolConfig = new PoolConfiguration { UseSsl = false };
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, ipEndpoint);
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
