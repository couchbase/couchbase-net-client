using System;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.DI
{
    public class SaslMechanismFactoryTests
    {
        [Theory]
        [InlineData(MechanismType.Plain, typeof(PlainSaslMechanism))]
        [InlineData(MechanismType.ScramSha256, typeof(ScramShaMechanism))]
        [InlineData(MechanismType.ScramSha512, typeof(ScramShaMechanism))]
        public void Create_GivenType_ExpectedType(MechanismType mechanismType, Type expectedType)
        {
            var username = "ted";
            var password = "secret";

            var saslMechanismFactory = new SaslMechanismFactory(
                new Mock<ILogger<PlainSaslMechanism>>().Object,
                new Mock<ILogger<ScramShaMechanism>>().Object,
                new Mock<ILogger<OAuthBearerSaslMechanism>>().Object,
                NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());

            var result = saslMechanismFactory.CreatePasswordMechanism(mechanismType, username, password);

            Assert.IsAssignableFrom(expectedType, result);
        }

        [Theory]
        [InlineData(MechanismType.OAuthBearer)]
        public void Create_UnsupportedType_ArgumentOutOfRangeException(MechanismType mechanismType)
        {
            var username = "ted";
            var password = "secret";

            var saslMechanismFactory = new SaslMechanismFactory(
                new Mock<ILogger<PlainSaslMechanism>>().Object,
                new Mock<ILogger<ScramShaMechanism>>().Object,
                new Mock<ILogger<OAuthBearerSaslMechanism>>().Object,
                NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                saslMechanismFactory.CreatePasswordMechanism(mechanismType, username, password));

            Assert.Equal("mechanismType", ex.ParamName);
        }

#if NET8_0_OR_GREATER
        [Fact]
        public void Create_ScramSha1_OnNet8Plus_ThrowsNotSupportedException()
        {
            // On .NET 8+, ScramSha1 is explicitly rejected: NIST SP 800-131A Rev 2 disallows
            // SHA-1 for new HMAC/PBKDF2 use and Rfc2898DeriveBytes.Pbkdf2 supports SHA-256/512.
            var saslMechanismFactory = new SaslMechanismFactory(
                new Mock<ILogger<PlainSaslMechanism>>().Object,
                new Mock<ILogger<ScramShaMechanism>>().Object,
                new Mock<ILogger<OAuthBearerSaslMechanism>>().Object,
                NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());

#pragma warning disable CS0618 // ScramSha1 is obsolete — intentional for this test
            var ex = Assert.Throws<NotSupportedException>(() =>
                saslMechanismFactory.CreatePasswordMechanism(MechanismType.ScramSha1, "ted", "secret"));
#pragma warning restore CS0618

            Assert.Contains("ScramSha1", ex.Message);
            Assert.Contains("ScramSha256", ex.Message);
        }
#endif

        // ── CreatePasswordMechanism(IConnection, enableTls, ...) — negotiation from cached list ──────────

        private static SaslMechanismFactory CreateFactory() => new(
            new Mock<ILogger<PlainSaslMechanism>>().Object,
            new Mock<ILogger<ScramShaMechanism>>().Object,
            new Mock<ILogger<OAuthBearerSaslMechanism>>().Object,
            NoopRequestTracer.Instance,
            Mock.Of<IOperationConfigurator>());

        private static IConnection ConnectionWith(string supportedSaslMechanisms)
        {
            var connection = new Mock<IConnection>();
            connection.SetupGet(c => c.SupportedSaslMechanisms).Returns(supportedSaslMechanisms);
            return connection.Object;
        }

        [Fact]
        public void CreatePasswordMechanism_Tls_ReturnsPlain_WithoutNegotiation()
        {
            // Over TLS, PLAIN is used directly; the server list is irrelevant (here it's even null).
            var result = CreateFactory().CreatePasswordMechanism(ConnectionWith(null), enableTls: true, "ted", "secret");

            Assert.IsType<PlainSaslMechanism>(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void CreatePasswordMechanism_NonTls_EmptyServerList_Throws(string serverList)
        {
            var ex = Assert.Throws<AuthenticationFailureException>(() =>
                CreateFactory().CreatePasswordMechanism(ConnectionWith(serverList), enableTls: false, "ted", "secret"));

            Assert.Contains("bootstrap", ex.Message);
        }

#if NET8_0_OR_GREATER
        [Theory]
        [InlineData("SCRAM-SHA512 SCRAM-SHA256 SCRAM-SHA1 PLAIN", MechanismType.ScramSha512)]
        [InlineData("SCRAM-SHA256 SCRAM-SHA1 PLAIN", MechanismType.ScramSha256)]
        public void CreatePasswordMechanism_NonTls_OnNet8Plus_SelectsStrongest(string serverList, MechanismType expected)
        {
            var result = CreateFactory().CreatePasswordMechanism(ConnectionWith(serverList), enableTls: false, "ted", "secret");

            var scram = Assert.IsType<ScramShaMechanism>(result);
            Assert.Equal(expected, scram.MechanismType);
        }

        [Fact]
        public void CreatePasswordMechanism_NonTls_OnNet8Plus_OnlySha1_Throws()
        {
            // Server offers only SHA-1, which is disabled on .NET 8+ → no common mechanism.
            var ex = Assert.Throws<AuthenticationFailureException>(() =>
                CreateFactory().CreatePasswordMechanism(ConnectionWith("SCRAM-SHA1 PLAIN"), enableTls: false, "ted", "secret"));

            Assert.Contains("No common SASL mechanism", ex.Message);
        }
#else
        [Fact]
        public void CreatePasswordMechanism_NonTls_OnNetStandard_SelectsSha1()
        {
            var result = CreateFactory().CreatePasswordMechanism(
                ConnectionWith("SCRAM-SHA512 SCRAM-SHA256 SCRAM-SHA1 PLAIN"), enableTls: false, "ted", "secret");

            var scram = Assert.IsType<ScramShaMechanism>(result);
#pragma warning disable CS0618
            Assert.Equal(MechanismType.ScramSha1, scram.MechanismType);
#pragma warning restore CS0618
        }
#endif
    }
}
