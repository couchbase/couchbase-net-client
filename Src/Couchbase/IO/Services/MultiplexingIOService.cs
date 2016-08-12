using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.IO.Services
{
    /// <summary>
    /// An IO service that dispatches without using a pool.
    /// </summary>
    public class MultiplexingIOService : IIOService
    {
        private static readonly ILog Log = LogManager.GetLogger<MultiplexingIOService>();
        private readonly IConnectionPool _connectionPool;

        private volatile bool _disposed;
        private readonly Guid _identity = Guid.NewGuid();
        private IConnection _connection;
        private readonly object _syncObj = new object();

        public MultiplexingIOService(IConnectionPool connectionPool)
        {
            Log.Debug(m=>m("Creating IOService {0}", _identity));
            _connectionPool = connectionPool;
            _connection = _connectionPool.Acquire();

            //authenticate the connection
            if (!_connection.IsAuthenticated)
            {
                Authenticate(_connection);
            }
        }

        public MultiplexingIOService(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
            : this(connectionPool)
        {
            SaslMechanism = saslMechanism;
        }

        /// <summary>
        /// Gets a value indicating whether enhanced durability is enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server supports enhanced durability and it is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsEnhancedDurability { get; private set; }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public ISaslMechanism SaslMechanism { get; set; }

        public bool IsSecure
        {
            get { return _connection != null && _connection.IsSecure; }
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            var request = operation.Write();
            var response = connection.Send(request);

            //Read the response and return the completed operation
            operation.Read(response, 0, response.Length);
            return operation.GetResultWithValue();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            var request = operation.Write();
            byte[] response = null;

            try
            {
                //A new connection will have to be authenticated
                if (!_connection.IsAuthenticated)
                {
                    //if two (or more) threads compete for auth, the first will succeed
                    //and subsequent threads will fail. This keeps that from happening.
                    lock (_syncObj)
                    {
                        Authenticate(_connection);
                        EnableEnhancedDurability(_connection);
                    }
                }

                response = _connection.Send(request);
            }
            catch (SocketException e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.TransportFailure);
            }
            catch (AuthenticationException e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.AuthenticationError);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
            }
            finally
            {
                //need better error handling
                if (_connection.IsDead)
                {
                    _connectionPool.Release(_connection);
                    _connection = _connectionPool.Acquire();
                }
            }

            //Read the response and return the completed operation
            if (response != null && response.Length > 0)
            {
                operation.Read(response, 0, response.Length);
            }
            return operation.GetResultWithValue();
        }

        public IOperationResult Execute(IOperation operation)
        {
            var request = operation.Write();
            byte[] response = null;

            try
            {
                //A new connection will have to be authenticated
                if (!_connection.IsAuthenticated)
                {
                    lock (_syncObj)
                    {
                        Authenticate(_connection);
                        EnableEnhancedDurability(_connection);
                    }
                }

                response = _connection.Send(request);
            }
            catch (SocketException e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.TransportFailure);
            }
            catch (AuthenticationException e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.AuthenticationError);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
            }
            finally
            {
                //need better error handling
                if (_connection.IsDead)
                {
                    _connectionPool.Release(_connection);
                    _connection = _connectionPool.Acquire();
                }
            }

            //Read the response and return the completed operation
            if (response != null && response.Length > 0)
            {
                operation.Read(response, 0, response.Length);
            }
            return operation.GetResult();
        }

        public async Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                var request = await operation.WriteAsync().ContinueOnAnyContext();
                connection.SendAsync(request, operation.Completed);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                //need better error handling
                if (connection.IsDead)
                {
                    _connectionPool.Release(connection);
                    _connection = _connectionPool.Acquire();
                }
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation);
            }
        }

        public async Task ExecuteAsync<T>(IOperation<T> operation)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                if (!_connection.IsAuthenticated)
                {
                    lock (_syncObj)
                    {
                        Authenticate(_connection);
                        EnableEnhancedDurability(_connection);
                    }
                }
                await ExecuteAsync(operation, _connection);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                if (_connection.IsDead)
                {
                    _connectionPool.Release(_connection);
                    _connection = _connectionPool.Acquire();
                }
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation);
            }
        }

        public async Task ExecuteAsync(IOperation operation, IConnection connection)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                var request = await operation.WriteAsync().ContinueOnAnyContext();
                connection.SendAsync(request, operation.Completed);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation);
            }
        }

        public async Task ExecuteAsync(IOperation operation)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                if (!_connection.IsAuthenticated)
                {
                    lock (_syncObj)
                    {
                        Authenticate(_connection);
                        EnableEnhancedDurability(_connection);
                    }
                }
                await ExecuteAsync(operation, _connection);
            }
            catch (Exception e)
            {
                Log.DebugFormat("Endpoint: {0} - {1} {2}", EndPoint, _identity, e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation);
            }
        }

        private static async Task HandleException(ExceptionDispatchInfo capturedException, IOperation operation)
        {
            var sourceException = capturedException.SourceException;
            var status = ResponseStatus.ClientFailure;
            if (sourceException is SocketException)
            {
                status = ResponseStatus.TransportFailure;
            }
            else if (sourceException is AuthenticationException)
            {
                status = ResponseStatus.AuthenticationError;
            }

            await operation.Completed(new SocketAsyncState
            {
                Exception = sourceException,
                Opaque = operation.Opaque,
                Status = status
            }).ContinueOnAnyContext();
        }

        private void Authenticate(IConnection connection)
        {
            if (!connection.IsAuthenticated)
            {
                if (SaslMechanism != null)
                {
                    var result = SaslMechanism.Authenticate(connection);
                    if (result)
                    {
                        Log.Debug(
                            m =>
                                m("Authenticated {0} using {1} - {2} [{3}].", SaslMechanism.Username, SaslMechanism.GetType(),
                                    _identity, EndPoint));
                        connection.IsAuthenticated = true;
                    }
                    else
                    {
                        Log.Debug(
                            m =>
                                m("Could not authenticate {0} using {1} - {2} [{3}].", SaslMechanism.Username,
                                    SaslMechanism.GetType(), _identity, EndPoint));
                        throw new AuthenticationException(ExceptionUtil.FailedBucketAuthenticationMsg.WithParams(SaslMechanism.Username));
                    }
                }
            }
        }

        /// <summary>
        /// Enables enhanced durability if it is configured and supported by the server.
        /// </summary>
        /// <param name="connection">The connection.</param>
        private void EnableEnhancedDurability(IConnection connection)
        {
            var features = new List<short>();

            var config = ConnectionPool.Configuration;
            if (config.UseEnhancedDurability)
            {
                features.Add((short) ServerFeatures.MutationSeqno);
                var hello = new Hello(features.ToArray(), new DefaultTranscoder(), 0, 0);

                var result = Execute(hello, connection);
                if (result.Success)
                {
                    SupportsEnhancedDurability = result.Value.Contains((short) ServerFeatures.MutationSeqno);
                }
                else
                {
                    LogFailedHelloOperation(result);
                }
            }
            else
            {
                var hello = new Hello(features.ToArray(), new DefaultTranscoder(), 0, 0);

                var result = Execute(hello, connection);
                if (!result.Success)
                {
                    LogFailedHelloOperation(result);
                }
            }
        }

        /// <summary>
        /// Logs a failed HELO operation
        /// </summary>
        /// <param name="result"></param>
        private static void LogFailedHelloOperation(IResult result)
        {
            Log.Debug(m => m(string.Format("Error when trying to execute HELO operation - {0} - {1}", result.Message, result.Exception)));
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Log.Debug(m => m("Disposing IOService for {0} - {1}", EndPoint, _identity));
            lock (_syncObj)
            {
                Dispose(true);
            }
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                if (_connectionPool != null)
                {
                    _connectionPool.Release(_connection);
                    _connectionPool.Dispose();
                }
            }
            _disposed = true;
        }

#if DEBUG
        ~MultiplexingIOService()
        {
            Log.Debug(m => m("Finalizing IOService for {0} - {1}", EndPoint, _identity));
            Dispose(false);
        }
#endif
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
