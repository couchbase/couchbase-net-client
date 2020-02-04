using Couchbase.Core.IO.Authentication;

#nullable enable

namespace Couchbase.Core.DI
{
    internal interface ISaslMechanismFactory
    {
        ISaslMechanism Create(MechanismType mechanismType, string username, string password);
    }
}
