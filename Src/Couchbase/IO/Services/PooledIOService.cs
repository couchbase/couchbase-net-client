using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.IO.Services
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// The default service for performing IO
    /// </summary>
    public class PooledIOService : IIOService
    {
        private static readonly ILog Log = LogManager.GetLogger<PooledIOService>();
        private readonly IConnectionPool _connectionPool;

        private volatile bool _disposed;
        private ISaslMechanism _saslMechanism;
        private readonly Guid _identity = Guid.NewGuid();
        private readonly object _syncObj = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledIOService"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        public PooledIOService(IConnectionPool connectionPool)
        {
            Log.Debug("Creating PooledIOService {0}", _identity);
            _connectionPool = connectionPool;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledIOService"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        /// <param name="saslMechanism">The sasl mechanism.</param>
        public PooledIOService(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
        {
            Log.Debug("Creating PooledIOService {0}", _identity);
            _connectionPool = connectionPool;
            _saslMechanism = saslMechanism;
        }

        /// <summary>
        /// Executes an operation for a given key.
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
        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            //Get the request buffer and send it
            var request = operation.Write();
            var response = connection.Send(request);

            //Read the response and return the completed operation
            operation.Read(response, 0, response.Length);
            return operation.GetResultWithValue();
        }

        /// <summary>
        /// Executes an operation for a given key.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> being executed.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> representing the result of operation.
        /// </returns>
        public IOperationResult Execute(IOperation operation)
        {
            //Get the buffer and a connection
            var request = operation.Write();
            var connection = _connectionPool.Acquire();
            byte[] response =  null;
            try
            {
                //A new connection will have to be authenticated
                if (!connection.IsAuthenticated)
                {
                    lock (_syncObj)
                    {
                        Authenticate(connection);
                        EnableServerFeatures(connection);
                    }
                }

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
                _connectionPool.Owner.MarkDead();
            }
            catch (Exception e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
            }
            finally
            {
                _connectionPool.Release(connection);
            }

            //Read the response and return the completed operation
            if (response != null && response.Length > 0)
            {
                operation.Read(response, 0, response.Length);
            }
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
        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            //Get the buffer and a connection
            var request = operation.Write();
            var connection = _connectionPool.Acquire();
            byte[] response = null;
            try
            {
                //A new connection will have to be authenticated
                if (!connection.IsAuthenticated)
                {
                    lock (_syncObj)
                    {
                        Authenticate(connection);
                        EnableServerFeatures(connection);
                    }
                }

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
                _connectionPool.Owner.MarkDead();
            }
            catch (Exception e)
            {
                Log.Debug(e);
                operation.Exception = e;
                operation.HandleClientError(e.Message, ResponseStatus.ClientFailure);
            }
            finally
            {
                _connectionPool.Release(connection);
            }

            //Read the response and return the completed operation
            if (response != null && response.Length > 0)
            {
                operation.Read(response, 0, response.Length);
            }
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
        public async Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
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
        public async Task ExecuteAsync<T>(IOperation<T> operation)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                var connection = _connectionPool.Acquire();
                if (!connection.IsAuthenticated)
                {
                    lock (_syncObj)
                    {
                        Authenticate(connection);
                        EnableServerFeatures(connection);
                    }
                }
                await ExecuteAsync(operation, connection);
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
        /// <param name="operation">The <see cref="IOperation{T}" /> being executed.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> representing the result of operation.
        /// </returns>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public async Task ExecuteAsync(IOperation operation)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                var connection = _connectionPool.Acquire();
                if (!connection.IsAuthenticated)
                {
                    lock (_syncObj)
                    {
                        Authenticate(connection);
                        EnableServerFeatures(connection);
                    }
                }
                await ExecuteAsync(operation, connection);
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

        /// <summary>
        /// The IP endpoint of the node in the cluster that this <see cref="IIOService" /> instance is communicating with.
        /// </summary>
        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        /// <summary>
        /// The <see cref="IConnectionPool" /> that this <see cref="IIOService" /> instance is using for acquiring <see cref="IConnection" />s.
        /// </summary>
        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        /// <summary>
        /// The SASL mechanism type the <see cref="IIOService" /> is using for authentication.
        /// </summary>
        /// <remarks>
        /// This could be PLAIN or CRAM-MD5 depending upon what the server supports.
        /// </remarks>
        public ISaslMechanism SaslMechanism
        {
            get { return _saslMechanism; }
            set { _saslMechanism = value; }
        }

        /// <summary>
        /// Authenticates the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <exception cref="System.Security.Authentication.AuthenticationException"></exception>
        private void Authenticate(IConnection connection)
        {
            if (!connection.IsAuthenticated)
            {
                if (_saslMechanism != null)
                {
                    var result = _saslMechanism.Authenticate(connection);
                    if (result)
                    {
                        Log.Debug(
                            "Authenticated {0} using {1} - {2}.", _saslMechanism.Username,
                                    _saslMechanism.GetType(), _identity);
                        connection.IsAuthenticated = true;
                    }
                    else
                    {
                        Log.Debug(
                            "Could not authenticate {0} using {1} - {2}.", _saslMechanism.Username,
                                    _saslMechanism.GetType(), _identity);
                        throw new AuthenticationException(ExceptionUtil.FailedBucketAuthenticationMsg.WithParams(SaslMechanism.Username));
                    }
                }
            }
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

            var hello = new Hello(features.ToArray(), new DefaultTranscoder(), 0, 0);

            var result = Execute(hello, connection);
            if (result.Success)
            {
                SupportsEnhancedDurability = result.Value.Contains((short) ServerFeatures.MutationSeqno);
                SupportsSubdocXAttributes = result.Value.Contains((short) ServerFeatures.SubdocXAttributes);
                SupportsEnhancedAuthentication = result.Value.Contains((short) ServerFeatures.SelectBucket);
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
            Log.Debug("Error when trying to execute HELO operation - {0} - {1}", result.Message, result.Exception);
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
        /// Returns true if internal TCP connections are using SSL.
        /// </summary>
        public bool IsSecure
        {
            get
            {
                var connection = _connectionPool.Acquire();
                var isSecure = connection.IsSecure;
                _connectionPool.Release(connection);
                return isSecure;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Log.Debug("Disposing PooledIOService for {0} - {1}", EndPoint, _identity);
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
                if (_connectionPool != null)
                {
                    _connectionPool.Dispose();
                }
            }
            _disposed = true;
        }

#if DEBUG
        ~PooledIOService()
        {
            Log.Debug("Finalizing PooledIOService for {0} - {1}", EndPoint, _identity);
            Dispose(false);
        }
#endif
    }
}
