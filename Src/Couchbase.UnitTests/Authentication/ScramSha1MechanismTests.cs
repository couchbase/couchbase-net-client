using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Authentication;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Authentication
{
    [TestFixture]
    public class ScramSha1MechanismTests
    {
        //SCRAM-Sha512 SCRAM-Sha256 SCRAM-SHA1 CRAM-MD5 PLAIN

        [Test]
        [TestCase("SCRAM-Sha512")]
        [TestCase("SCRAM-Sha256")]
        [TestCase("SCRAM-SHA1")]
        public void SaslFactory_WhenSCRAM_SHA1Available_FuncReturnsScramShaMechanism(string mechanismType)
        {
            //arrange
            var config = new PoolConfiguration(new ClientConfiguration
            {
                ForceSaslPlain = false
            });

            var connection = new Mock<IConnection>();
            var connectionPool = new Mock<IConnectionPool>();
            connectionPool.Setup(x => x.Acquire()).Returns(connection.Object);
            connectionPool.Setup(x => x.Configuration).Returns(config);

            var opResult = new Mock<IOperationResult<string>>();
            opResult.Setup(x => x.Success).Returns(true);
            opResult.Setup(x => x.Value).Returns("SCRAM-Sha512 SCRAM-Sha256 SCRAM-SHA1 CRAM-MD5 PLAIN");

            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var service = new Mock<IIOService>();
            service.Setup(x => x.ConnectionPool).Returns(connectionPool.Object);
            service.Setup(x => x.Execute(It.IsAny<SaslList>(), It.IsAny<IConnection>()))
                .Returns(opResult.Object);

            SaslFactory.Execute = (op, conn) => opResult.Object;

            //act
            var factory = SaslFactory.GetFactory();
            var mechanism = factory("authenticated", "secret", connectionPool.Object, transcoder);

            //assert
            Assert.IsTrue(mechanism is ScramShaMechanism);
            Assert.AreEqual("SCRAM-SHA1", mechanism.MechanismType);
        }

        [Test]
        public void ScramShaMechanism_WhenTranscoderIsNullinCtor_ThrowArgumentNullException()
        {
            ITypeTranscoder transcoder = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new ScramShaMechanism(transcoder, MechanismType.ScramSha256));
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ScramShaMechanism_WhenUsernameIsInvalidinCtor_ThrowArgumentNullException(string username)
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            string password = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            // ReSharper disable once ObjectCreationAsStatement
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.Throws<ArgumentNullException>(() => new ScramShaMechanism(transcoder, username, password, MechanismType.ScramSha256));
        }

        [Test]
        [TestCase("PLAIN")]
        [TestCase("CRAM-MD5")]
        public void ScramShaMechanism_WhenMechanismIsInavlid_ThrowArgumentOutOfRangeException(string mechanismType)
        {
            ITypeTranscoder transcoder = new DefaultTranscoder(new DefaultConverter());
            string username = "beef";
            string password = "stew";

            // ReSharper disable once ExpressionIsAlwaysNull
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentOutOfRangeException>(() => new ScramShaMechanism(transcoder, username, password, mechanismType));
        }

        [Test]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(null)]
        public void ScramShaMechanism_WhenMechanismIsInavlid_ThrowArgumentNullException(string mechanismType)
        {
            ITypeTranscoder transcoder = new DefaultTranscoder(new DefaultConverter());
            string username = "beef";
            string password = "stew";

            // ReSharper disable once ExpressionIsAlwaysNull
            // ReSharper disable once ObjectCreationAsStatement
            Assert.Throws<ArgumentNullException>(() => new ScramShaMechanism(transcoder, username, password, mechanismType));
        }

        [Test]
        public void ScramShaMechanism_ClientNonce_IsPopulated()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            string password = "secret";
            string username = "authenticated";

            var mech = new ScramShaMechanism(transcoder, username, password, MechanismType.ScramSha256);
            Assert.That(() => !string.IsNullOrEmpty(mech.ClientNonce));
        }

        [Test]
        public void ScramShaMechanism_Authenticate_IsPopulated()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            string password = "secret";
            string username = "authenticated";

            var mech = new ScramShaMechanism(transcoder, username, password, MechanismType.ScramSha256);
            Assert.That(() => !string.IsNullOrEmpty(mech.ClientNonce));
        }

        [TearDown]
        public static void TearDown()
        {
#if NET45
            //resets Execute back to original func
            typeof(SaslFactory).TypeInitializer.Invoke(null, null);
#endif
        }
    }
}
