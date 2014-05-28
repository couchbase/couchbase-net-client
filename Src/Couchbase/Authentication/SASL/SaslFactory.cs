using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.IO;

namespace Couchbase.Authentication.SASL
{
    internal static class SaslFactory
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();

        public static Func<string, string, string, ISaslMechanism> GetFactory()
        {
            return (username, password, mechanism) =>
            {
                ISaslMechanism saslMechanism;
                SaslMechanismType mechanismType;
                if (Enum.TryParse(mechanism, true, out mechanismType))
                {
                    switch (mechanismType)
                    {
                        case SaslMechanismType.Plain:
                            saslMechanism = new PlainTextMechanism(username, password);
                            break;
                        case SaslMechanismType.CramMd5:
                            saslMechanism = new CramMd5Mechanism(username, password);
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

        public static Func<string, string, SaslMechanismType, ISaslMechanism> GetFactory2()
        {
            return (username, password, mechanismType) =>
            {
                Log.Debug(m => m("Using {0} Sasl Mechanism for authentication.", mechanismType));
                ISaslMechanism saslMechanism;
                switch (mechanismType)
                {
                    case SaslMechanismType.Plain:
                        saslMechanism = new PlainTextMechanism(username, password);
                        break;
                    case SaslMechanismType.CramMd5:
                        saslMechanism = new CramMd5Mechanism(username, password);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return saslMechanism;
            };
        } 
    }
}
