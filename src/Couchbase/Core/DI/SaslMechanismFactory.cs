using System;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Operations;
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
                MechanismType.ScramSha1 => (ISaslMechanism) new ScramShaMechanism(mechanismType, password,
                    username, _scramLogger, _tracer, _operationConfigurator),
                MechanismType.Plain => new PlainSaslMechanism(username, password, _plainLogger, _tracer, _operationConfigurator),
                _ => throw new ArgumentOutOfRangeException(nameof(mechanismType))
            };
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
