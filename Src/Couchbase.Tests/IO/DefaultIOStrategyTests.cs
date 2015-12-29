using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Services;
using Couchbase.Tests.Fakes;
using Couchbase.Tests.IO.Operations;
using Couchbase.Utils;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Couchbase.Tests.IO
{
    [TestFixture]
    public class PooledIOServiceTests
    {
        private IIOService _ioService;
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
            _ioService = new PooledIOService(_connectionPool, null);
        }

        [Test]
        public void When_Authentication_Fails_AuthenticationException_Or_ConnectionUnavailableException_Is_Thrown()
        {
            var authenticator = new CramMd5Mechanism(_ioService, "authenticated", "secretw", new DefaultTranscoder());
            _ioService.SaslMechanism = authenticator;

            //The first two iterations will throw auth exceptions and then a CUE;
            //you will never find yourself in an infinite loop waiting for connections that don't exist.
            int count = 0;
            while (count < 3)
            {
                count++;
                try
                {
                    var config = new Config(new DefaultTranscoder(), OperationLifespan, UriExtensions.GetEndPoint(Address));
                    var result = _ioService.Execute(config);
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

        [Test]
        public void Test_ExecuteAsync()
        {
            var tcs = new TaskCompletionSource<object>();
            var operation = new Noop( new DefaultTranscoder(), OperationLifespan);
            operation.Completed = s =>
            {
                Assert.IsNull(s.Exception);

                var buffer = s.Data.ToArray();
                operation.Read(buffer, 0, buffer.Length);
                var result = operation.GetResult();
                Assert.IsTrue(result.Success);
                Assert.IsNull(result.Exception);
                Assert.IsNullOrEmpty(result.Message);
                tcs.SetResult(result);
                return tcs.Task;
            };
        }

        [Test]
        public void When_Operation_Fails_With_SocketException_TransportFailure_Is_Returned()
        {
            var mockedSasl = new Mock<ISaslMechanism>();

            var mockedConnection = new Mock<IConnection>();
            mockedConnection.Setup(x => x.Send(It.IsAny<byte[]>())).Throws<SocketException>();
            mockedConnection.Setup(x => x.IsAuthenticated).Returns(true);

            var mockedPool = new Mock<IConnectionPool>();
            mockedPool.Setup(x => x.Acquire()).Returns(mockedConnection.Object);

            var service = new PooledIOService(mockedPool.Object, mockedSasl.Object);
            var op = new Get<object>(string.Empty, null, new DefaultTranscoder(new DefaultConverter()), 100);
            var result = service.Execute(op);

            Assert.AreEqual(ResponseStatus.TransportFailure, result.Status);
        }

        [Test]
        public void When_Operation_Fails_Without_SocketException_ClientFailure_Is_Returned()
        {
            var mockedSasl = new Mock<ISaslMechanism>();

            var mockedConnection = new Mock<IConnection>();
            mockedConnection.Setup(x => x.Send(It.IsAny<byte[]>())).Throws<IndexOutOfRangeException>();
            mockedConnection.Setup(x => x.IsAuthenticated).Returns(true);

            var mockedPool = new Mock<IConnectionPool>();
            mockedPool.Setup(x => x.Acquire()).Returns(mockedConnection.Object);

            var service = new PooledIOService(mockedPool.Object, mockedSasl.Object);
            var op = new Add<string>(string.Empty, "", null, new DefaultTranscoder(new DefaultConverter()), 100);
            var result = service.Execute(op);

            Assert.AreEqual(ResponseStatus.ClientFailure, result.Status);
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _ioService.Dispose();
        }
    }
}
