using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Couchbase.Tracing;
using Couchbase.Utils;

namespace Couchbase.IO.Services
{
    /// <summary>
    /// An IO service that dispatches without using a pool.
    /// </summary>
    public class MultiplexingIOService : IOServiceBase
    {
        private static readonly ILog Log = LogManager.GetLogger<MultiplexingIOService>();
        private volatile bool _disposed;
        private IConnection _connection;
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(true);

        public MultiplexingIOService(IConnectionPool connectionPool)
        {
            Log.Info("Creating IOService {0}", Identity);
            ConnectionPool = connectionPool;
            _connection = connectionPool.Connections.FirstOrDefault() ?? connectionPool.Acquire();

            //enable the server features
            CheckEnabledServerFeatures(_connection);
            EnableServerFeatures(_connection);
        }

        public MultiplexingIOService(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
            : this(connectionPool)
        {
            SaslMechanism = saslMechanism;
        }

        /// <summary>
        /// Returns true if internal TCP connections are using SSL.
        /// </summary>
        /// <exception cref="NotSupportedException"></exception>
        public override bool IsSecure
        {
            get => _connection != null && _connection.IsSecure;
            protected set => throw new NotSupportedException();
        }

        /// <summary>
        /// Executes the specified operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation">The operation.</param>
        /// <returns></returns>
        /// <exception cref="TransportFailureException"></exception>
        public override IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            try
            {
                if (_connection.IsConnected)
                {
                    var request = operation.Write(Tracer, ConnectionPool.Configuration.BucketName);
                    byte[] response;
                    OperationHeader header;
                    ErrorCode errorCode;

                    using (var scope = Tracer.BuildSpan(operation, _connection, ConnectionPool.Configuration.BucketName).StartActive())
                    {
                        response = _connection.Send(request);
                        header = response.CreateHeader(ErrorMap, out errorCode);
                        scope.Span.SetPeerLatencyTag(header.GetServerDuration(response));
                    }

                    operation.Read(response, header, errorCode);
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, Identity, _connection.Identity, e);
                HandleException(e, operation);
            }
            finally
            {
                if (_connection.IsDead)
                {
                    CheckConnection();
                }
            }

            return operation.GetResultWithValue(Tracer, ConnectionPool.Configuration.BucketName);
        }

        /// <summary>
        /// Executes the specified operation.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <returns></returns>
        /// <exception cref="TransportFailureException"></exception>
        public override IOperationResult Execute(IOperation operation)
        {
            try
            {
                if (_connection.IsConnected)
                {
                    var request = operation.Write(Tracer, ConnectionPool.Configuration.BucketName);
                    byte[] response;
                    OperationHeader header;
                    ErrorCode errorCode;

                    using (var scope = Tracer.BuildSpan(operation, _connection, ConnectionPool.Configuration.BucketName).StartActive())
                    {
                        response = _connection.Send(request);
                        header = response.CreateHeader(ErrorMap, out errorCode);
                        scope.Span.SetPeerLatencyTag(header.GetServerDuration(response));
                    }

                    operation.Read(response, header, errorCode);
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, Identity, _connection.Identity, e);
                HandleException(e, operation);
            }
            finally
            {
                if (_connection.IsDead)
                {
                    CheckConnection();
                }
            }

            return operation.GetResult(Tracer, ConnectionPool.Configuration.BucketName);
        }

        /// <summary>
        /// Executes the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation">The operation.</param>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
        /// <exception cref="TransportFailureException"></exception>
        public override async Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                if (connection.IsConnected)
                {
                    var request = await operation.WriteAsync(Tracer, ConnectionPool.Configuration.BucketName).ContinueOnAnyContext();
                    var span = Tracer.BuildSpan(operation, connection, ConnectionPool.Configuration.BucketName).Start();
                    await connection.SendAsync(request, operation.Completed, span, ErrorMap).ContinueOnAnyContext();
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, Identity, connection.Identity, e);
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
                await HandleException(capturedException, operation, EndPoint).ContinueOnAnyContext();
            }
        }

        /// <summary>
        /// Executes the asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation">The operation.</param>
        /// <returns></returns>
        public override Task ExecuteAsync<T>(IOperation<T> operation)
        {
            return ExecuteAsync(operation, _connection);
        }

        /// <summary>
        /// Executes the asynchronous.
        /// </summary>
        /// <param name="operation">The operation.</param>
        /// <param name="connection">The connection.</param>
        /// <returns></returns>
        /// <exception cref="TransportFailureException"></exception>
        public override async Task ExecuteAsync(IOperation operation, IConnection connection)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                if (connection.IsConnected)
                {
                    var request = await operation.WriteAsync(Tracer, ConnectionPool.Configuration.BucketName).ContinueOnAnyContext();
                    var span = Tracer.BuildSpan(operation, connection, ConnectionPool.Configuration.BucketName).Start();
                    connection.SendAsync(request, operation.Completed, span, ErrorMap);
                }
                else
                {
                    throw new TransportFailureException(ExceptionUtil.GetMessage(ExceptionUtil.NotConnectedMsg, EndPoint));
                }
            }
            catch (Exception e)
            {
                Log.Info("Endpoint: {0} - {1} - {2} {3}", EndPoint, Identity, connection.Identity, e);
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
                await HandleException(capturedException, operation, EndPoint).ContinueOnAnyContext();
            }
        }

        public override Task ExecuteAsync(IOperation operation)
        {
            return ExecuteAsync(operation, _connection);
        }

        /// <summary>
        /// Checks the connection.
        /// </summary>
        void CheckConnection()
        {
            var lockTaken = false;
            try
            {
                Monitor.TryEnter(SyncObj, ref lockTaken);
                if (!lockTaken) return;
                IConnection connection = null;
                try
                {
                    Log.Info("Checking connection {0} is dead {1}", _connection.Identity, _connection.IsDead);
                    if (_connection == null || _connection.IsDead)
                    {
                        Log.Info("Trying to acquire a new connection for {0}", _connection.Identity,
                            _connection.IsDead);
                        ConnectionPool.Release(_connection);

                        connection = ConnectionPool.Acquire();
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
                        ConnectionPool.Release(connection);
                        Log.Info("Connection {0} {1}", _connection.Identity, e);
                    }
                }
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(SyncObj);
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            Log.Info("Disposing IOService for {0} - {1}", EndPoint, Identity);
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
                if (ConnectionPool != null)
                {
                    ConnectionPool.Release(_connection);
                    ConnectionPool.Dispose();
                }
                _resetEvent?.Dispose();
            }
        }

#if DEBUG
        ~MultiplexingIOService()
        {
            Log.Info("Finalizing IOService for {0} - {1}", EndPoint, Identity);
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
