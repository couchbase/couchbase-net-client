using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Services;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class SaslFactoryTests
    {
        private PooledIOService _ioService;
        private IConnectionPool<Connection> _connectionPool;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(_address);
            var connectionPoolConfig = new PoolConfiguration { UseSsl = false };
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, ipEndpoint);
            _ioService = new PooledIOService(_connectionPool);
        }

        [Test]
        public void Test_GetFactory()
        {
            var factory = SaslFactory.GetFactory();
            Assert.IsNotNull(factory);
        }

        [Test]
        public void When_PlainText_Provided_Factory_Returns_ScramShaMechanism()
        {
            var factory = SaslFactory.GetFactory();
            var mechanism = factory("authenticated", "secret", _ioService, new DefaultTranscoder());
            Assert.IsTrue(mechanism is ScramShaMechanism);
        }
    }
}
