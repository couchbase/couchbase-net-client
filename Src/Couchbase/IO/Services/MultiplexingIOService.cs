using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
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
        private ErrorMap _errorMap;
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(true);

        public MultiplexingIOService(IConnectionPool connectionPool)
        {
            Log.Info("Creating IOService {0}", _identity);
            _connectionPool = connectionPool;
            _connection = _connectionPool.Acquire();

            //enable the server features
            EnableServerFeatures(_connection);
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

        /// <summary>
        /// Gets a value indicating whether Subdocument XAttributes are supported.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server supports Subdocument XAttributes; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsSubdocXAttributes { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the cluster supports Enhanced Authentication.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cluster supports enhanced authentication; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsEnhancedAuthentication { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the cluster supports an error map that can
        /// be used to return custom error information.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cluster supports KV error map; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsKvErrorMap { get; private set; }

        /// <summary>
        /// The Error Map that is used to map error codes from the server to error messages.
        /// </summary>
        public ErrorMap ErrorMap { get; internal set; }

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
            operation.Read(response, ErrorMap);
            return operation.GetResultWithValue();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            var request = operation.Write();
            byte[] response = null;

            try
            {
                if (_connection.IsConnected)
                {
                    response = _connection.Send(request);
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, _identity, _connection.Identity, e);
                HandleException(e, operation);
            }
            finally
            {
                if (_connection.IsDead)
                {
                    CheckConnection();
                }
            }

            operation.Read(response, ErrorMap);
            return operation.GetResultWithValue();//might have to handle a special null case her
        }

        public IOperationResult Execute(IOperation operation)
        {
            var request = operation.Write();
            byte[] response = null;

            try
            {
                if (_connection.IsConnected)
                {
                    response = _connection.Send(request);
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, _identity, _connection.Identity, e);
                HandleException(e, operation);
            }
            finally
            {
                if (_connection.IsDead)
                {
                    CheckConnection();
                }
            }

            operation.Read(response, ErrorMap);
            return operation.GetResult();
        }

        public async Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                if (connection.IsConnected)
                {
                    var request = await operation.WriteAsync().ContinueOnAnyContext();
                    connection.SendAsync(request, operation.Completed);
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, _identity, connection.Identity, e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                if (_connection.IsDead)
                {
                    CheckConnection();
                }
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation, EndPoint);
            }
        }

        public Task ExecuteAsync<T>(IOperation<T> operation)
        {
            return ExecuteAsync(operation, _connection);
        }

        public async Task ExecuteAsync(IOperation operation, IConnection connection)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                if (connection.IsConnected)
                {
                    var request = await operation.WriteAsync().ContinueOnAnyContext();
                    connection.SendAsync(request, operation.Completed);
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, _identity, connection.Identity, e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                if (_connection.IsDead)
                {
                    CheckConnection();
                }
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation, EndPoint);
            }
        }

        public Task ExecuteAsync(IOperation operation)
        {
            return ExecuteAsync(operation, _connection);
        }

        private static async Task HandleException(ExceptionDispatchInfo capturedException, IOperation operation, IPEndPoint endPoint)
        {
            var sourceException = capturedException.SourceException;
            var status = ResponseStatus.ClientFailure;
            if (sourceException is SocketException ||
                sourceException is TransportFailureException ||
                sourceException is SendTimeoutExpiredException)
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
                Status = status,
                EndPoint = endPoint
            }).ContinueOnAnyContext();
        }

        private static void HandleException(Exception capturedException, IOperation operation)
        {
            var status = ResponseStatus.ClientFailure;
            if (capturedException is SocketException ||
                capturedException is TransportFailureException ||
                capturedException is SendTimeoutExpiredException)
            {
                status = ResponseStatus.TransportFailure;
            }
            else if (capturedException is AuthenticationException)
            {
                status = ResponseStatus.AuthenticationError;
            }
            operation.Exception = capturedException;
            operation.HandleClientError(capturedException.Message, status);
        }

        /// <summary>
        /// Send request to server to try and enable server features.
        /// </summary>
        /// <param name="connection">The connection.</param>
        private void EnableServerFeatures(IConnection connection)
        {
            var features = new List<short>
            {
                (short) ServerFeatures.SubdocXAttributes,
                (short) ServerFeatures.SelectBucket
            };

            if (ConnectionPool.Configuration.UseEnhancedDurability)
            {
                features.Add((short) ServerFeatures.MutationSeqno);
            }
            if (ConnectionPool.Configuration.UseKvErrorMap)
            {
                features.Add((short) ServerFeatures.XError);
            }

            var transcoder = new DefaultTranscoder();
            var hello = new Hello(features.ToArray(), transcoder, 0, 0);

            var result = Execute(hello, connection);
            if (result.Success)
            {
                SupportsEnhancedDurability = result.Value.Contains((short) ServerFeatures.MutationSeqno);
                SupportsSubdocXAttributes = result.Value.Contains((short) ServerFeatures.SubdocXAttributes);
                SupportsEnhancedAuthentication = result.Value.Contains((short) ServerFeatures.SelectBucket);
                SupportsKvErrorMap = result.Value.Contains((short) ServerFeatures.XError);

                if (SupportsKvErrorMap)
                {
                    var errorMapResult = Execute(new GetErrorMap(transcoder, 0));
                    if (!errorMapResult.Success)
                    {
                        throw new Exception("Error retrieving error map. Cluster indicated it was available.");
                    }

                    ErrorMap = errorMapResult.Value;
                }
            }
            else
            {
                LogFailedHelloOperation(result);
            }
        }

        /// <summary>
        /// Logs a failed HELO operation
        /// </summary>
        /// <param name="result"></param>
        private static void LogFailedHelloOperation(IResult result)
        {
            Log.Info("Error when trying to execute HELO operation - {0} - {1}", result.Message, result.Exception);
        }

        void CheckConnection()
        {
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(_syncObj, ref lockTaken);
                if (!lockTaken) return;
                IConnection connection = null;
                try
                {
                    Log.Info("Checking connection {0} is dead {1}", _connection.Identity, _connection.IsDead);
                    if (_connection == null || _connection.IsDead)
                    {
                        Log.Info("Trying to acquire a new connection for {0}", _connection.Identity,
                            _connection.IsDead);
                        _connectionPool.Release(_connection);

                        connection = _connectionPool.Acquire();
                        Log.Info("Exchanging {0} for {1}", _connection.Identity, connection.Identity);
                        Interlocked.Exchange(ref _connection, connection);

                        EnableServerFeatures(_connection);
                    }
                }
                catch (Exception e)
                {
                    if (connection != null)
                    {
                        connection.IsDead = true;
                        _connectionPool.Release(connection);
                        Log.Info("Connection {0} {1}", _connection.Identity, e);
                    }
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_syncObj);
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Log.Info("Disposing IOService for {0} - {1}", EndPoint, _identity);
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                if (_connectionPool != null)
                {
                    _connectionPool.Release(_connection);
                    _connectionPool.Dispose();
                }
                if (_resetEvent != null)
                {
                    _resetEvent.Dispose();
                }
            }
        }

#if DEBUG
        ~MultiplexingIOService()
        {
            Log.Info("Finalizing IOService for {0} - {1}", EndPoint, _identity);
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
