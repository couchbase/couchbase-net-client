using System;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Transcoders;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    internal class SaslMechanismFactory : ISaslMechanismFactory
    {
        private readonly ITypeTranscoder _transcoder;
        private readonly ILogger<PlainSaslMechanism> _plainLogger;
        private readonly ILogger<ScramShaMechanism> _scramLogger;

        public SaslMechanismFactory(ILogger<PlainSaslMechanism> plainLogger,
            ILogger<ScramShaMechanism> scramLogger)
        {
            _transcoder = new LegacyTranscoder(); //required so that SASL strings are not JSON encoded
            _plainLogger = plainLogger;
            _scramLogger = scramLogger;
        }

        public ISaslMechanism Create(MechanismType mechanismType, string username, string password)
        {
            return mechanismType switch
            {
                MechanismType.ScramSha1 => (ISaslMechanism) new ScramShaMechanism(_transcoder, mechanismType, password,
                    username, _scramLogger),
                MechanismType.Plain => new PlainSaslMechanism(username, password, _plainLogger),
                _ => throw new ArgumentOutOfRangeException(nameof(mechanismType))
            };
        }
    }
}
