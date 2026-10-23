using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Authentication.Authenticators
{
    public class PasswordAuthenticatorTests
    {
        private const string ServerMechanisms = "SCRAM-SHA512 SCRAM-SHA256 SCRAM-SHA1 PLAIN";

        // NCBC-4331: the mechanism used to come from a constructor flag that defaulted to true, so an
        // authenticator built by hand (or swapped in to rotate credentials) sent PLAIN on couchbase://.

        [Fact]
        public async Task AuthenticateKv_NonTlsConnection_DoesNotUsePlain()
        {
            var chosen = await AuthenticateAsync(new PasswordAuthenticator("ted", "secret"), isSecure: false);

            Assert.IsNotType<PlainSaslMechanism>(chosen);
            Assert.IsType<ScramShaMechanism>(chosen);
        }

        [Fact]
        public async Task AuthenticateKv_NonTlsConnection_IgnoresDeprecatedEnableTlsFlag()
        {
            var authenticator = new PasswordAuthenticator("ted", "secret", enableTls: true);

            var chosen = await AuthenticateAsync(authenticator, isSecure: false);

            Assert.IsType<ScramShaMechanism>(chosen);
        }

        [Fact]
        public async Task AuthenticateKv_TlsConnection_UsesPlain()
        {
            var authenticator = new PasswordAuthenticator("ted", "secret", enableTls: false);

            var chosen = await AuthenticateAsync(authenticator, isSecure: true);

            Assert.IsType<PlainSaslMechanism>(chosen);
        }

        private static async Task<ISaslMechanism> AuthenticateAsync(PasswordAuthenticator authenticator, bool isSecure)
        {
            var connection = new Mock<IConnection>();
            connection.SetupGet(c => c.IsSecure).Returns(isSecure);
            connection.SetupGet(c => c.SupportedSaslMechanisms).Returns(isSecure ? null : ServerMechanisms);

            var factory = new RecordingSaslMechanismFactory();
            await authenticator.AuthenticateKvConnectionAsync(connection.Object, factory, CancellationToken.None);

            Assert.NotNull(factory.Chosen);
            return factory.Chosen;
        }

        /// <summary>
        /// Lets the real factory choose the mechanism, records it, and hands back a stub so nothing is
        /// sent on the mocked connection.
        /// </summary>
        private sealed class RecordingSaslMechanismFactory : ISaslMechanismFactory
        {
            private readonly SaslMechanismFactory _inner = new(
                new Mock<ILogger<PlainSaslMechanism>>().Object,
                new Mock<ILogger<ScramShaMechanism>>().Object,
                new Mock<ILogger<OAuthBearerSaslMechanism>>().Object,
                NoopRequestTracer.Instance,
                Mock.Of<IOperationConfigurator>());

            public ISaslMechanism Chosen { get; private set; }

            public ISaslMechanism CreatePasswordMechanism(IConnection connection, string username, string password)
            {
                Chosen = _inner.CreatePasswordMechanism(connection, username, password);
                return Mock.Of<ISaslMechanism>();
            }

            public ISaslMechanism CreatePasswordMechanism(MechanismType mechanismType, string username, string password) =>
                _inner.CreatePasswordMechanism(mechanismType, username, password);

            public ISaslMechanism CreateOAuthBearerMechanism(string token) => _inner.CreateOAuthBearerMechanism(token);
        }
    }
}
