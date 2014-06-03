using System;
using Common.Logging;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Creates an ISaslMechanism to use for authenticating Couchbase Clients.
    /// </summary>
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

        /// <summary>
        /// Gets a factory for creating an ISaslMechanism.
        /// </summary>
        /// <returns>An ISaslMechanism for authenticating connectivity to a Couchbase Bucket.</returns>
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
        
        public static Func<string, string, IOStrategy, ISaslMechanism> GetFactory3()
        {
            return (username, password, strategy) =>
            {
                ISaslMechanism saslMechanism = null;
                var connection = strategy.ConnectionPool.Acquire();
                try
                {
                    var saslListResult = strategy.Execute(new SaslList(), connection);
                    if (saslListResult.Success)
                    {
                        if (saslListResult.Value.Contains("CRAM-MD5"))
                        {
                            saslMechanism = new CramMd5Mechanism(strategy ,username, password);
                        }
                        else
                        {
                            saslMechanism = new PlainTextMechanism(strategy, username, password);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                if (saslMechanism != null)
                {
                    saslMechanism.IOStrategy = strategy;
                }
                return saslMechanism;
            };
        } 
    }
}
