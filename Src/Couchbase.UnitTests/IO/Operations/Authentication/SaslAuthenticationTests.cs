using System.Text;
using Couchbase.Authentication.SASL;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Services;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.Authentication
{
    [TestFixture]
    public class SaslAuthenticationTests
    {
        private const string Username = "foo";
        private const string Password = "bar";
        private const string BucketName = "default";

        private IConnectionPool _connectionPool;
        private const uint OperationLifespan = 2500; //ms

        [OneTimeSetUp]
        public void Setup()
        {
            var mockConnection = new Mock<IConnection>();
            mockConnection.SetupGet(x => x.IsAuthenticated).Returns(false);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);

            _connectionPool = mockConnectionPool.Object;
        }

        [Test]
        public void When_SaslMechinism_Is_Set_On_PoolIOService_And_Authentication_Fails_Return_AuthenticationError()
        {
            var mockAuthenticator = new Mock<ISaslMechanism>();
            mockAuthenticator.Setup(x => x.Authenticate(It.IsAny<IConnection>(), Username, Password))
                .Returns(false);
            mockAuthenticator.SetupGet(x => x.Username).Returns(BucketName);

            var service = new PooledIOService(_connectionPool, mockAuthenticator.Object);
            var operation = new SaslStart("PLAIN", GetAuthData(Username, Password), new DefaultTranscoder(), OperationLifespan);
            var response = service.Execute(operation);

            Assert.AreEqual(string.Format("Authentication failed for bucket '{0}'", BucketName), response.Message);
            Assert.AreEqual(ResponseStatus.AuthenticationError, response.Status);
            Assert.IsFalse(response.Success);
        }

        [Test]
        public void When_SaslMechinism_Is_Set_On_MultiplexingIOService_And_Authentication_Fails_Return_AuthenticationError()
        {
            var mockAuthenticator = new Mock<ISaslMechanism>();
            mockAuthenticator.Setup(x => x.Authenticate(It.IsAny<IConnection>(), Username, Password))
                .Returns(false);
            mockAuthenticator.SetupGet(x => x.Username).Returns(BucketName);

            var service = new MultiplexingIOService(_connectionPool, mockAuthenticator.Object);
            var operation = new SaslStart("PLAIN", GetAuthData(Username, Password), new DefaultTranscoder(), OperationLifespan);
            var response = service.Execute(operation);

            Assert.AreEqual(string.Format("Authentication failed for bucket '{0}'", BucketName), response.Message);
            Assert.AreEqual(ResponseStatus.AuthenticationError, response.Status);
            Assert.IsFalse(response.Success);
        }

        private static string GetAuthData(string userName, string passWord)
        {
            const string empty = "\0";
            var sb = new StringBuilder();
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(passWord);
            return sb.ToString();
        }
    }
}
