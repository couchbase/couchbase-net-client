using System;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Security.Authentication;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Couchbase.Utils;

namespace Couchbase.Tests.Fakes
{
    internal class FakeIOService : IIOService
    {
        public FakeIOService(IPEndPoint endPoint, IConnectionPool connectionPool, bool isSecure)
        {
            EndPoint = endPoint;
            ConnectionPool = connectionPool;
            IsSecure = isSecure;
        }

        public IPEndPoint EndPoint { get; private set; }

        public IConnectionPool ConnectionPool { get; private set; }

        public ISaslMechanism SaslMechanism { get; set; }

        public bool IsSecure { get; private set; }

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
            var connection = ConnectionPool.Acquire();
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
               ConnectionPool.Release(connection);
            }

            //Read the response and return the completed operation
            if (response != null)
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
            var connection = ConnectionPool.Acquire();
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
                ConnectionPool.Release(connection);
            }

            //Read the response and return the completed operation
            if (response != null)
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
            try
            {
                var request = await operation.WriteAsync().ContinueOnAnyContext();
                connection.SendAsync(request, operation.Completed);
            }
            catch (Exception e)
            {
                var completed = operation.Completed;
                completed(new SocketAsyncState
                {
                    Exception = e,
                    Opaque = operation.Opaque
                });
            }
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
            ExceptionDispatchInfo capturedException = null;
            try
            {
                var request = await operation.WriteAsync().ContinueOnAnyContext();
                connection.SendAsync(request, operation.Completed);
            }
            catch (Exception e)
            {
                capturedException = ExceptionDispatchInfo.Capture(e);
            }

            if (capturedException != null)
            {
                await operation.Completed(new SocketAsyncState
                {
                    Exception = capturedException.SourceException,
                    Opaque = operation.Opaque
                }).ContinueOnAnyContext();
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
        public Task ExecuteAsync<T>(IOperation<T> operation)
        {
            var connection = ConnectionPool.Acquire();
            if (!connection.IsAuthenticated)
            {
                Authenticate(connection);
            }
            return ExecuteAsync(operation, connection);
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
        public Task ExecuteAsync(IOperation operation)
        {
            var connection = ConnectionPool.Acquire();
            if (!connection.IsAuthenticated)
            {
                Authenticate(connection);
            }
            return ExecuteAsync(operation, connection);
        }

        /// <summary>
        /// Authenticates the specified connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <exception cref="System.Security.Authentication.AuthenticationException"></exception>
        private void Authenticate(IConnection connection)
        {
            connection.IsAuthenticated = true;
        }

        public void Dispose()
        {
        }

        public ErrorMap ErrorMap { get; protected set; }


        public bool SupportsEnhancedDurability { get; protected set; }

        public bool SupportsSubdocXAttributes { get; protected set; }

        public bool SupportsEnhancedAuthentication { get; protected set; }

        public bool SupportsKvErrorMap { get; protected set; }
    }
}