using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;
using Couchbase.Logging;
using Couchbase.Utils;

namespace Couchbase.IO.Services
{
    /// <summary>
    /// An <see cref="IIOService"/> implementation which shares MUX connections across threads.
    /// </summary>
    /// <seealso cref="Couchbase.IO.Services.PooledIOService" />
    public class SharedPooledIOService : PooledIOService
    {
        private static readonly ILog Log = LogManager.GetLogger<SharedPooledIOService>();

        public SharedPooledIOService(IConnectionPool connectionPool)
            : base(connectionPool)
        {
        }

        public SharedPooledIOService(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
            : base(connectionPool, saslMechanism)
        {
        }

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <param name="operation">The <see cref="T:Couchbase.IO.Operations.IOperation`1" /> being executed.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult" /> representing the result of operation.
        /// </returns>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public override async Task ExecuteAsync(IOperation operation)
        {
            ExceptionDispatchInfo capturedException = null;
            IConnection connection = null;
            try
            {
                connection = ConnectionPool.Acquire();

                Log.Trace("Using conn {0} on {1}", connection.Identity, connection.EndPoint);

                //A new connection will have to check for server features
                CheckEnabledServerFeatures(connection);

                await ExecuteAsync(operation, connection).ContinueOnAnyContext();
            }
            catch (Exception e)
            {
                Log.Debug(e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                ConnectionPool.Release(connection);
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation).ContinueOnAnyContext();
            }
        }

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="T:Couchbase.IO.Operations.IOperation`1" /> being executed.</param>
        /// <returns>
        /// An <see cref="T:Couchbase.IOperationResult`1" /> representing the result of operation.
        /// </returns>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public override async Task ExecuteAsync<T>(IOperation<T> operation)
        {
            ExceptionDispatchInfo capturedException = null;
            IConnection connection = null;
            try
            {
                connection = ConnectionPool.Acquire();

                Log.Trace("Using conn {0} on {1}", connection.Identity, connection.EndPoint);

                //A new connection will have to check for server features
                CheckEnabledServerFeatures(connection);

                await ExecuteAsync(operation, connection).ContinueOnAnyContext();
            }
            catch (Exception e)
            {
                Log.Debug(e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                ConnectionPool.Release(connection);
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation).ContinueOnAnyContext();
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
