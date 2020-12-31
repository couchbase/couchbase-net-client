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
        private readonly IRequestTracer _tracer;
        private readonly IOperationConfigurator _operationConfigurator;

        public SaslMechanismFactory(ILogger<PlainSaslMechanism> plainLogger,
            ILogger<ScramShaMechanism> scramLogger,
            IRequestTracer tracer,
            IOperationConfigurator operationConfigurator)
        {
            _plainLogger = plainLogger;
            _scramLogger = scramLogger;
            _tracer = tracer;
            _operationConfigurator = operationConfigurator;
            _plainLogger = plainLogger;
        }

        public ISaslMechanism Create(MechanismType mechanismType, string username, string password)
        {
            return mechanismType switch
            {
                MechanismType.ScramSha1 => (ISaslMechanism) new ScramShaMechanism(mechanismType, password,
                    username, _scramLogger, _tracer, _operationConfigurator),
                MechanismType.Plain => new PlainSaslMechanism(username, password, _plainLogger, _tracer, _operationConfigurator),
                _ => throw new ArgumentOutOfRangeException(nameof(mechanismType))
            };
        }
    }
}
