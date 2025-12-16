using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Diagnostics;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Implements SASL OAUTHBEARER mechanism for JWT authentication.
    /// This is a single-step mechanism that sends the JWT token to the server.
    /// </summary>
    internal sealed class OAuthBearerSaslMechanism : SaslMechanismBase
    {
        private readonly string _token;

        public OAuthBearerSaslMechanism(
            string token,
            ILogger<OAuthBearerSaslMechanism> logger,
            IRequestTracer tracer,
            IOperationConfigurator operationConfigurator)
            : base(tracer, operationConfigurator)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("Token cannot be null or empty", nameof(token));
            }

            _token = token;
            Logger = logger;
            MechanismType = MechanismType.OAuthBearer;
        }

        /// <inheritdoc />
        public override async Task AuthenticateAsync(IConnection connection, CancellationToken cancellationToken = default)
        {
            using var rootSpan = Tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Internal.SaslStart);

            try
            {
                // OAUTHBEARER is a single-step mechanism
                // Format: n,,\x01auth=Bearer <token>\x01\x01
                // where \x01 is the ASCII "start of heading" character (byte value 1)
                // (Need to concatenate else the compiler will interpret \x01auth as `\0x1a uth`)
                var payload = $"n,,\x01" + $"auth=Bearer {_token}\x01\x01";

                Logger?.LogDebug("Authenticating using OAUTHBEARER mechanism");

                _ = await SaslStart(connection, payload, rootSpan, cancellationToken).ConfigureAwait(false);

                Logger?.LogDebug("OAUTHBEARER authentication completed successfully");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "OAUTHBEARER authentication failed");
                throw;
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
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
