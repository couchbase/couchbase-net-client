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
using Couchbase.IO.Operations.Errors;
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
        protected readonly IConnectionPool _connectionPool;

        private volatile bool _disposed;
        protected readonly Guid Identity = Guid.NewGuid();
        protected readonly object SyncObj = new object();
        private ErrorMap _errorMap;
        protected volatile bool MustEnableServerFeatures = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledIOService"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        public PooledIOService(IConnectionPool connectionPool)
        {
            Log.Debug("Creating PooledIOService {0}", Identity);
            _connectionPool = connectionPool;

            var conn = _connectionPool.Connections.FirstOrDefault();
            CheckEnabledServerFeatures(conn);
            _connectionPool.SupportsEnhancedAuthentication = SupportsEnhancedAuthentication;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PooledIOService"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        /// <param name="saslMechanism">The sasl mechanism.</param>
        public PooledIOService(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
        {
            Log.Debug("Creating PooledIOService {0}", Identity);
            _connectionPool = connectionPool;
            SaslMechanism = saslMechanism;
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
            operation.Read(response, ErrorMap);
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

            Log.Trace("Using conn {0} on {1}", connection.Identity, connection.EndPoint);
            byte[] response =  null;
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
        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            //Get the buffer and a connection
            var request = operation.Write();
            var connection = _connectionPool.Acquire();

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
        public virtual async Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
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
        public virtual async Task ExecuteAsync(IOperation operation, IConnection connection)
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
        public virtual async Task ExecuteAsync<T>(IOperation<T> operation)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                var connection = _connectionPool.Acquire();

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
        public virtual async Task ExecuteAsync(IOperation operation)
        {
            ExceptionDispatchInfo capturedException = null;
            try
            {
                var connection = _connectionPool.Acquire();

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

            if (capturedException != null)
            {
                await HandleException(capturedException, operation);
            }
        }

        protected static async Task HandleException(ExceptionDispatchInfo capturedException, IOperation operation)
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
        public ISaslMechanism SaslMechanism { get; set; }

        /// <summary>
        /// Send request to server to try and enable server features.
        /// </summary>
        /// <param name="connection">The connection.</param>
        protected void EnableServerFeatures(IConnection connection)
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
            var result = Execute(new Hello(features.ToArray(), transcoder, 0, 0), connection);
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
        /// Checks the that the server features have been enabled on the <see cref="IConnection"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        protected void CheckEnabledServerFeatures(IConnection connection)
        {
            if (!connection.MustEnableServerFeatures) return;
            lock (SyncObj)
            {
                EnableServerFeatures(connection);
                connection.MustEnableServerFeatures = false;
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
            Log.Debug("Finalizing PooledIOService for {0} - {1}", EndPoint, Identity);
            Dispose(false);
        }
#endif
    }
}
