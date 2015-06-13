using System;
using System.Net;
using System.Security.Authentication;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.IO.Strategies
{
    // ReSharper disable once InconsistentNaming
    /// <summary>
    /// The default strategy for performing IO
    /// </summary>
    internal class DefaultIOStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetLogger<DefaultIOStrategy>();
        private readonly IConnectionPool _connectionPool;

        private volatile bool _disposed;
        private ISaslMechanism _saslMechanism;
        private readonly Guid _identity = Guid.NewGuid();

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultIOStrategy"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        public DefaultIOStrategy(IConnectionPool connectionPool)
        {
            Log.Debug(m=>m("Creating DefaultIOStrategy {0}", _identity));
            _connectionPool = connectionPool;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultIOStrategy"/> class.
        /// </summary>
        /// <param name="connectionPool">The connection pool.</param>
        /// <param name="saslMechanism">The sasl mechanism.</param>
        public DefaultIOStrategy(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
        {
            Log.Debug(m => m("Creating DefaultIOStrategy {0}", _identity));
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
        /// <exception cref="System.NotImplementedException"></exception>
        public IOperationResult Execute(IOperation operation)
        {
            //Get the buffer and a connection
            var request = operation.Write();
            var connection = _connectionPool.Acquire();
            byte[] response;
            try
            {
                //A new connection will have to be authenticated
                if (!connection.IsAuthenticated)
                {
                    Authenticate(connection);
                }

                //Send the request buffer and release the connection
                response = connection.Send(request);
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
                    Authenticate(connection);
                }

                //Send the request buffer and release the connection
                response = connection.Send(request);
            }
            catch (Exception e)
            {
                Log.Debug(e);
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
        /// <exception cref="System.NotImplementedException"></exception>
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
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public async Task ExecuteAsync(IOperation operation, IConnection connection)
        {
            try
            {
                var request = await operation.WriteAsync().ContinueOnAnyContext();
                connection.SendAsync(request, operation.Completed);
            }
            catch (Exception e)
            {
                Log.Debug(e);
                operation.Completed(new SocketAsyncState
                {
                    Exception = e,
                    Opaque = operation.Opaque,
                    Status = ResponseStatus.ClientFailure
                });
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
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public async Task ExecuteAsync<T>(IOperation<T> operation)
        {
            try
            {
                var connection = _connectionPool.Acquire();
                if (!connection.IsAuthenticated)
                {
                    Authenticate(connection);
                }
                await ExecuteAsync(operation, connection);
            }
            catch (Exception e)
            {
                Log.Debug(e);
                operation.Completed(new SocketAsyncState
                {
                    Exception = e,
                    Opaque = operation.Opaque,
                    Status = ResponseStatus.ClientFailure
                });
            }
        }

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation{T}" /> being executed.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> representing the result of operation.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        /// <remarks>
        /// This overload is used to perform authentication on the connection if it has not already been authenticated.
        /// </remarks>
        public async Task ExecuteAsync(IOperation operation)
        {
            try
            {
                var connection = _connectionPool.Acquire();
                if (!connection.IsAuthenticated)
                {
                    Authenticate(connection);
                }
                await ExecuteAsync(operation, connection);
            }
             catch (Exception e)
             {
                 Log.Debug(e);
                 operation.Completed(new SocketAsyncState
                 {
                     Exception = e,
                     Opaque = operation.Opaque,
                     Status = ResponseStatus.ClientFailure
                 });
             }
        }


        /// <summary>
        /// The IP endpoint of the node in the cluster that this <see cref="IOStrategy" /> instance is communicating with.
        /// </summary>
        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        /// <summary>
        /// The <see cref="IConnectionPool" /> that this <see cref="IOStrategy" /> instance is using for acquiring <see cref="IConnection" />s.
        /// </summary>
        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        /// <summary>
        /// The SASL mechanism type the <see cref="IOStrategy" /> is using for authentication.
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
            if (_saslMechanism != null)
            {
                var result = _saslMechanism.Authenticate(connection);
                if (result)
                {
                    Log.Debug(m => m("Authenticated {0} using {1} - {2}.", _saslMechanism.Username, _saslMechanism.GetType(), _identity));
                    connection.IsAuthenticated = true;
                }
                else
                {
                    Log.Debug(m => m("Could not authenticate {0} using {1} - {2}.", _saslMechanism.Username, _saslMechanism.GetType(), _identity));
                    throw new AuthenticationException(_saslMechanism.Username);
                }
            }
        }

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
            Log.Debug(m => m("Disposing DefaultIOStrategy for {0} - {1}", EndPoint, _identity));
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
        ~DefaultIOStrategy()
        {
            Log.Debug(m => m("Finalizing DefaultIOStrategy for {0} - {1}", EndPoint, _identity));
            Dispose(false);
        }
#endif
    }
}
