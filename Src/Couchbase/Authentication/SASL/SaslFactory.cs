using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;

namespace Couchbase.Authentication.SASL
{
    internal static class SaslFactory
    {
        public static Func<IOStrategy, string, ISaslMechanism> GetFactory()
        {
            return (strategy, mechanism) =>
            {
                ISaslMechanism saslMechanism;
                SaslMechanismType mechanismType;
                if (Enum.TryParse(mechanism, true, out mechanismType))
                {
                    switch (mechanismType)
                    {
                        case SaslMechanismType.Plain:
                            saslMechanism = new PlainTextMechanism(strategy);
                            break;
                        case SaslMechanismType.CramMd5:
                            saslMechanism = new CramMd5Mechanism(strategy);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    throw new NotSupportedException(mechanism);
                }
                return saslMechanism;
            };
        } 
    }
}
