using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Authentication
{
    [TestFixture]
    public class SaslFactoryTests
    {
        [Test]
        public void SaslFactory_Returns_PlainMechanism_If_ForceSaslPlain_Is_True()
        {
            var config = new PoolConfiguration(new ClientConfiguration
            {
                ForceSaslPlain = true
            });

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Configuration).Returns(config);

            var result = SaslFactory.GetFactory()("", "", mockConnectionPool.Object, null);

            Assert.IsInstanceOf<PlainTextMechanism>(result);
            mockConnectionPool.Verify(x => x.Acquire(), Times.Never);
        }
    }
}
