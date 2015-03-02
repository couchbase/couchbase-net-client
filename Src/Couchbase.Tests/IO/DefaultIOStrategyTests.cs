using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies;
using Couchbase.Tests.IO.Operations;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.IO
{
    [TestFixture]
    public class DefaultIOStrategyTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private static readonly string Address = ConfigurationManager.AppSettings["OperationTestAddress"];
        private const uint OperationLifespan = 2500; //ms

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, ipEndpoint);
            _connectionPool.Initialize();
            _ioStrategy = new DefaultIOStrategy(_connectionPool, null);
        }

        [Test]
        public void When_Authentication_Fails_AuthenticationException_Or_ConnectionUnavailableException_Is_Thrown()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy, "authenticated", "secretw", new AutoByteConverter());
            _ioStrategy.SaslMechanism = authenticator;

            //The first two iterations will throw auth exceptions and then a CUE;
            //you will never find yourself in an infinite loop waiting for connections that don't exist.
            int count = 0;
            while (count < 3)
            {
                count++;
                try
                {
                    var config = new Config(new AutoByteConverter(), UriExtensions.GetEndPoint(Address), OperationLifespan);
                    var result = _ioStrategy.Execute(config);
                    Console.WriteLine(result.Success);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    var type = e.GetType();
                    if (type == typeof (AuthenticationException) || type == typeof (ConnectionUnavailableException))
                    {
                        continue;
                    }
                    Assert.Fail();
                }
            }
            Assert.Pass();
        }
    }
}
