using System;
using Microsoft.Extensions.Logging;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using Couchbase.Utils;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Creates an ISaslMechanism to use for authenticating Couchbase Clients.
    /// </summary>
    internal static class SaslFactory
    {
        private static readonly ILogger Log = new LoggerFactory().CreateLogger("SaslFactory");

        /// <summary>
        /// The default timeout for SASL-related operations.
        /// </summary>
        public const uint DefaultTimeout = 2500; //2.5sec

        public static Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> GetFactory()
        {
            return (username, password, strategy, transcoder) =>
            {
                ISaslMechanism saslMechanism = null;
                IConnection connection = null;
                try
                {
                    connection = strategy.ConnectionPool.Acquire();
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
                    if (connection != null)
                    {
                        strategy.ConnectionPool.Release(connection);
                    }
                }
                return saslMechanism;
            };
        }
    }
}
