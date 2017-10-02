using System;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.IO.Services
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// The default service for performing IO. Each thread uses a connection before returning back to the pool.
    /// </summary>
    /// <seealso cref="Couchbase.IO.Services.IOServiceBase" />
    public class PooledIOService : IOServiceBase
    {
        private static readonly ILog Log = LogManager.GetLogger<PooledIOService>();
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledIOService"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        public PooledIOService(IConnectionPool connectionPool)
        {
            Log.Debug("Creating PooledIOService {0}", Identity);
            ConnectionPool = connectionPool;

            var conn = ConnectionPool.Connections.FirstOrDefault();
            CheckEnabledServerFeatures(conn);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledIOService"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        /// <param name="saslMechanism">The sasl mechanism.</param>
        public PooledIOService(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
        {
            Log.Debug("Creating PooledIOService {0}", Identity);
            ConnectionPool = connectionPool;
            SaslMechanism = saslMechanism;

            var conn = ConnectionPool.Connections.FirstOrDefault();
            CheckEnabledServerFeatures(conn);
        }

        /// <summary>
        /// Executes an operation for a given key.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> being executed.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> representing the result of operation.
        /// </returns>
        public override IOperationResult Execute(IOperation operation)
        {
            //Get the buffer and a connection
            var request = operation.Write();
            var connection = ConnectionPool.Acquire();

            Log.Trace("Using conn {0} on {1}", connection.Identity, connection.EndPoint);
            byte[] response = null;
            try
            {
                //A new connection will have to check for server features
                CheckEnabledServerFeatures(connection);

                //Send the request buffer and release the connection
                response = connection.Send(request);
            }
            catch (SocketException e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.TransportFailure);
            }
            catch (AuthenticationException e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.AuthenticationError);
            }
            catch (RemoteHostTimeoutException e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.TransportFailure);

                //this almost always will be a server offline or service down
                ConnectionPool.Owner.MarkDead();
            }
            catch (Exception e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
            }
            finally
            {
                ConnectionPool.Release(connection);
            }

            operation.Read(response, ErrorMap);
            return operation.GetResult();
        }

        /// <summary>
        /// Executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> being executed.</param>
        /// <returns>
        /// An <see cref="IOperationResult{T}" /> representing the result of operation.
        /// </returns>
        public override IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            //Get the buffer and a connection
            var request = operation.Write();
            var connection = ConnectionPool.Acquire();

            Log.Trace("Using conn {0} on {1}", connection.Identity, connection.EndPoint);

            byte[] response = null;
            try
            {
                //A new connection will have to check for server features
                CheckEnabledServerFeatures(connection);

                //Send the request buffer and release the connection
                response = connection.Send(request);
            }
            catch (SocketException e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.TransportFailure);
            }
            catch (AuthenticationException e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.AuthenticationError);
            }
            catch (RemoteHostTimeoutException e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.TransportFailure);

                //this almost always will be a server offline or service down
                ConnectionPool.Owner.MarkDead();
            }
            catch (Exception e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
            }
            finally
            {
                ConnectionPool.Release(connection);
            }

            operation.Read(response, ErrorMap);
            return operation.GetResultWithValue();
        }

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> being executed.</param>
        /// <param name="connection">The <see cref="IConnection" /> the operation is using.</param>
        /// <returns>
        /// An <see cref="IOperationResult{T}" /> representing the result of operation.
        /// </returns>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public override async Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            var request = await operation.WriteAsync().ContinueOnAnyContext();
            connection.SendAsync(request, operation.Completed);
        }

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation{T}" /> being executed.</param>
        /// <param name="connection">The <see cref="IConnection" /> the operation is using.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> representing the result of operation.
        /// </returns>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public override async Task ExecuteAsync(IOperation operation, IConnection connection)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                Log.Trace("Using conn {0} on {1}", connection.Identity, connection.EndPoint);

                var request = await operation.WriteAsync().ContinueOnAnyContext();
                connection.SendAsync(request, operation.Completed);
            }
            catch (Exception e)
            {
                Log.Debug(e);
                capturedException = ExceptionDispatchInfo.Capture(e);
            }

            if (capturedException != null)
            {
                await HandleException(capturedException, operation);
            }
        }

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> being executed.</param>
        /// <returns>
        /// An <see cref="IOperationResult{T}" /> representing the result of operation.
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

                await ExecuteAsync(operation, connection);
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
                await HandleException(capturedException, operation);
            }
        }

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation{T}" /> being executed.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> representing the result of operation.
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

                await ExecuteAsync(operation, connection);
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
                await HandleException(capturedException, operation);
            }
        }

        /// <summary>
        /// Returns true if internal TCP connections are using SSL.
        /// </summary>
        public override bool IsSecure
        {
            get
            {
                var connection = ConnectionPool.Acquire();
                var isSecure = connection.IsSecure;
                ConnectionPool.Release(connection);
                return isSecure;
            }
            protected set => throw new NotSupportedException();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            Log.Debug("Disposing PooledIOService for {0} - {1}", EndPoint, Identity);
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                ConnectionPool?.Dispose();
            }
            _disposed = true;
        }

#if DEBUG
        /// <summary>Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.</summary>
        ~PooledIOService()
        {
            Log.Debug("Finalizing PooledIOService for {0} - {1}", EndPoint, Identity);
            Dispose(false);
        }
#endif
    }
}
