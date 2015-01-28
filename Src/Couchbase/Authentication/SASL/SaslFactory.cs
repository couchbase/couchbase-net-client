using System;
using Common.Logging;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Authentication;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Creates an ISaslMechanism to use for authenticating Couchbase Clients.
    /// </summary>
    internal static class SaslFactory
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();

        public static Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> GetFactory3()
        {
            return (username, password, strategy, converter) =>
            {
                ISaslMechanism saslMechanism = null;
                var connection = strategy.ConnectionPool.Acquire();
                try
                {
                    var saslListResult = strategy.Execute(new SaslList(converter), connection);
                    if (saslListResult.Success)
                    {
                        if (saslListResult.Value.Contains("CRAM-MD5"))
                        {
                            saslMechanism = new CramMd5Mechanism(strategy ,username, password, converter);
                        }
                        else
                        {
                            saslMechanism = new PlainTextMechanism(strategy, username, password, converter);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                return saslMechanism;
            };
        } 
    }
}
