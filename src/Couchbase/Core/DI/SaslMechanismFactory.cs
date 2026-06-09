using System;
using System.Linq;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    internal class SaslMechanismFactory : ISaslMechanismFactory
    {
        private readonly ILogger<PlainSaslMechanism> _plainLogger;
        private readonly ILogger<ScramShaMechanism> _scramLogger;
        private readonly ILogger<OAuthBearerSaslMechanism> _oauthLogger;
        private readonly IRequestTracer _tracer;
        private readonly IOperationConfigurator _operationConfigurator;

        public SaslMechanismFactory(
            ILogger<PlainSaslMechanism> plainLogger,
            ILogger<ScramShaMechanism> scramLogger,
            ILogger<OAuthBearerSaslMechanism> oauthLogger,
            IRequestTracer tracer,
            IOperationConfigurator operationConfigurator)
        {
            _plainLogger = plainLogger;
            _scramLogger = scramLogger;
            _oauthLogger = oauthLogger;
            _tracer = tracer;
            _operationConfigurator = operationConfigurator;
        }

        public ISaslMechanism CreatePasswordMechanism(MechanismType mechanismType, string username, string password)
        {
            return mechanismType switch
            {
                MechanismType.ScramSha512 => (ISaslMechanism) new ScramShaMechanism(mechanismType, password,
                    username, _scramLogger, _tracer, _operationConfigurator),
                MechanismType.ScramSha256 => new ScramShaMechanism(mechanismType, password,
                    username, _scramLogger, _tracer, _operationConfigurator),
#if NET8_0_OR_GREATER
                // ScramSha1 is explicitly rejected on .NET 8+: NIST SP 800-131A Rev 2 disallows SHA-1
                // for new HMAC/PBKDF2 use, and Rfc2898DeriveBytes.Pbkdf2 supports SHA-256/512.
#pragma warning disable CS0618
                MechanismType.ScramSha1 => throw new NotSupportedException(
                    "ScramSha1 is not supported on .NET 8 or later. Use ScramSha256 or ScramSha512."),
#pragma warning restore CS0618
#else
                // ScramSha1 is the only valid non-TLS mechanism on netstandard: Rfc2898DeriveBytes does not
                // support SHA-256/512 PBKDF2 prior to .NET 8. Suppressed intentionally — internal use only.
#pragma warning disable CS0618
                MechanismType.ScramSha1 => new ScramShaMechanism(mechanismType, password,
                    username, _scramLogger, _tracer, _operationConfigurator),
#pragma warning restore CS0618
#endif
                MechanismType.Plain => new PlainSaslMechanism(username, password, _plainLogger, _tracer, _operationConfigurator),
                _ => throw new ArgumentOutOfRangeException(nameof(mechanismType))
            };
        }

        public ISaslMechanism CreatePasswordMechanism(IConnection connection, bool enableTls, string username, string password)
        {
            // PLAIN is safe over TLS and needs no negotiation.
            if (enableTls)
            {
                return CreatePasswordMechanism(MechanismType.Plain, username, password);
            }

            // Non-TLS: negotiate against the server's advertised mechanisms (cached at bootstrap), selecting the
            // strongest the client supports on this target framework (ScramShaMechanism.ClientSupportedMechanisms).
            var serverMechanisms = connection.SupportedSaslMechanisms;
            if (string.IsNullOrEmpty(serverMechanisms))
            {
                throw new AuthenticationFailureException(
                    "No SASL mechanisms were negotiated for this connection. SASL_LIST_MECHS is fetched during " +
                    "connection bootstrap for non-TLS connections; an empty list indicates the connection was not " +
                    "initialized correctly.");
            }

            if (!ScramShaMechanism.TrySelectMechanism(serverMechanisms!, out var mechanismType))
            {
                throw new AuthenticationFailureException(
                    $"No common SASL mechanism. Server advertises [{serverMechanisms}]; this client supports " +
                    $"[{string.Join(", ", ScramShaMechanism.ClientSupportedMechanisms.Select(m => m.GetDescription()))}]. " +
#if NET8_0_OR_GREATER
                    "SCRAM-SHA-1 is disabled on .NET 8+ (NIST SP 800-131A Rev 2); enable SCRAM-SHA-256 or " +
                    "SCRAM-SHA-512 on Couchbase Server.");
#else
                    "Ensure SCRAM-SHA-1 is enabled on Couchbase Server, or run on .NET 8+ for SCRAM-SHA-256/512.");
#endif
            }

            return CreatePasswordMechanism(mechanismType, username, password);
        }

        public ISaslMechanism CreateOAuthBearerMechanism(string token)
        {
            return new OAuthBearerSaslMechanism(token, _oauthLogger, _tracer, _operationConfigurator);
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
