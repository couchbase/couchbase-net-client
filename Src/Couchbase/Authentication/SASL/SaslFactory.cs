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

        public static Func<string, string, IIOService, ITypeTranscoder, ISaslMechanism> GetFactory()
        {
            return (username, password, service, transcoder) =>
            {
                ISaslMechanism saslMechanism = null;
                IConnection connection = null;
                try
                {
                    connection = service.ConnectionPool.Acquire();
                    var saslListResult = service.Execute(new SaslList(transcoder, DefaultTimeout), connection);
                    if (saslListResult.Success)
                    {
                        if (saslListResult.Value.Contains("CRAM-MD5"))
                        {
                            saslMechanism = new CramMd5Mechanism(service, username, password, transcoder);
                        }
                        else
                        {
                            saslMechanism = new PlainTextMechanism(service, username, password, transcoder);
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
                        service.ConnectionPool.Release(connection);
                    }
                }
                return saslMechanism;
            };
        }
    }
}
