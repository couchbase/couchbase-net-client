using System;
using Common.Logging;
using Couchbase.Core.Transcoders;
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
        private readonly static ILog Log = LogManager.GetLogger("SaslFactory");

        /// <summary>
        /// The default timeout for SASL-related operations.
        /// </summary>
        public const uint DefaultTimeout = 2500; //2.5sec

        public static Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> GetFactory3()
        {
            return (username, password, strategy, transcoder) =>
            {
                ISaslMechanism saslMechanism = null;
                var connection = strategy.ConnectionPool.Acquire();
                try
                {
                    var saslListResult = strategy.Execute(new SaslList(transcoder, DefaultTimeout), connection);
                    if (saslListResult.Success)
                    {
                        if (saslListResult.Value.Contains("CRAM-MD5"))
                        {
                            saslMechanism = new CramMd5Mechanism(strategy, username, password, transcoder);
                        }
                        else
                        {
                            saslMechanism = new PlainTextMechanism(strategy, username, password, transcoder);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
                finally
                {
                    strategy.ConnectionPool.Release(connection);
                }
                return saslMechanism;
            };
        }
    }
}
