using System;
using Couchbase.Logging;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Authentication;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Creates an ISaslMechanism to use for authenticating Couchbase Clients.
    /// </summary>
    internal static class SaslFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SaslFactory));

        /// <summary>
        /// The default timeout for SASL-related operations.
        /// </summary>
        public const uint DefaultTimeout = 2500; //2.5sec

        public static Func<string, string, IConnectionPool, ITypeTranscoder, ISaslMechanism> GetFactory()
        {
            return (username, password, pool, transcoder) =>
            {
                ISaslMechanism saslMechanism = null;
                IConnection connection = null;
                try
                {
                    connection = pool.Acquire();
                    var saslListResult = Execute(new SaslList(transcoder, DefaultTimeout), connection);
                    if (saslListResult.Success)
                    {
                        if (saslListResult.Value.Contains("SCRAM-SHA1"))
                        {
                            return new ScramShaMechanism(transcoder, username, password, MechanismType.ScramSha1);
                        }
                        if (saslListResult.Value.Contains("CRAM-MD5"))
                        {
                            return new CramMd5Mechanism(username, password, transcoder);
                        }
                        if (saslListResult.Value.Contains("PLAIN"))
                        {
                            return new PlainTextMechanism(username, password, transcoder);
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
                        pool.Release(connection);
                    }
                }
                return saslMechanism;
            };
        }

         public static Func<IOperation<string>, IConnection, IOperationResult<string>> Execute = (op , conn ) =>
         {
             var request = op.Write();
             var response = conn.Send(request);
             op.Read(response, null);
             return op.GetResultWithValue();
         };
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
