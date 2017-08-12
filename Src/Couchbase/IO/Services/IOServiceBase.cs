using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Couchbase.Core.Transcoders;
using System.Runtime.ExceptionServices;
using System.Net.Sockets;
using System.Security.Authentication;
using Couchbase.Utils;
using Couchbase.Logging;

namespace Couchbase.IO.Services
{
    /// <summary>
    /// Base implementation for an <see cref="IIOService"/> class.
    /// </summary>
    /// <seealso cref="Couchbase.IO.IIOService" />
    public abstract class IOServiceBase : IIOService
    {
        private static readonly ILog Log = LogManager.GetLogger<IOServiceBase>();

        /// <summary>
        /// The synchronization object
        /// </summary>
        protected readonly object SyncObj = new object();

        /// <summary>
        /// A unique identifier for this instance.
        /// </summary>
        protected readonly Guid Identity = Guid.NewGuid();

        /// <summary>
        /// True if the client must enable server features.
        /// </summary>
        protected volatile bool MustEnableServerFeatures = true;

        /// <summary>
        /// The IP endpoint of the node in the cluster that this <see cref="T:Couchbase.IO.IIOService" /> instance is communicating with.
        /// </summary>
        public IPEndPoint EndPoint => ConnectionPool.EndPoint;

        /// <summary>
        /// The <see cref="T:Couchbase.IO.IConnectionPool" /> that this <see cref="T:Couchbase.IO.IIOService" /> instance is using for acquiring <see cref="T:Couchbase.IO.IConnection" />s.
        /// </summary>
        public IConnectionPool ConnectionPool { get; protected set; }

        /// <summary>
        /// The SASL mechanism type the <see cref="T:Couchbase.IO.IIOService" /> is using for authentication.
        /// </summary>
        /// <remarks>
        /// This could be PLAIN or CRAM-MD5 depending upon what the server supports.
        /// </remarks>
        public ISaslMechanism SaslMechanism { get; set; }

        /// <summary>
        /// Returns true if internal TCP connections are using SSL.
        /// </summary>
        public abstract bool IsSecure { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the server supports enhanced durability.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server supports enhanced durability; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsEnhancedDurability { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether [supports subdoc x attributes].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [supports subdoc x attributes]; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsSubdocXAttributes { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the cluster supports Enhanced Authentication.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cluster supports enhanced authentication; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsEnhancedAuthentication { get; protected set; }

        /// <summary>
        /// Gets a value indicating whether the cluster supports an error map that can
        /// be used to return custom error information.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the cluster supports KV error map; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsKvErrorMap { get; protected set; }

        /// <summary>
        /// The Error Map that is used to map error codes from the server to error messages.
        /// </summary>
        public ErrorMap ErrorMap { get; internal set; }

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
                features.Add((short)ServerFeatures.MutationSeqno);
            }
            if (ConnectionPool.Configuration.UseKvErrorMap)
            {
                features.Add((short)ServerFeatures.XError);
            }

            var transcoder = new DefaultTranscoder();
            var result = Execute(new Hello(features.ToArray(), transcoder, 0, 0), connection);
            if (result.Success)
            {
                SupportsEnhancedDurability = result.Value.Contains((short)ServerFeatures.MutationSeqno);
                SupportsSubdocXAttributes = result.Value.Contains((short)ServerFeatures.SubdocXAttributes);
                SupportsEnhancedAuthentication = result.Value.Contains((short)ServerFeatures.SelectBucket);
                SupportsKvErrorMap = result.Value.Contains((short)ServerFeatures.XError);
                ConnectionPool.SupportsEnhancedAuthentication = SupportsEnhancedAuthentication;

                if (SupportsKvErrorMap)
                {
                    var errorMapResult = Execute(new GetErrorMap(transcoder, 0), connection);
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

        protected static void HandleException(Exception capturedException, IOperation operation)
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
        /// Handles the exception.
        /// </summary>
        /// <param name="capturedException">The captured exception.</param>
        /// <param name="operation">The operation.</param>
        /// <returns></returns>
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
        /// Handles the exception.
        /// </summary>
        /// <param name="capturedException">The captured exception.</param>
        /// <param name="operation">The operation.</param>
        /// <param name="endPoint">The end point.</param>
        /// <returns></returns>
        protected static async Task HandleException(ExceptionDispatchInfo capturedException, IOperation operation, IPEndPoint endPoint)
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

        public abstract void Dispose();
        public abstract IOperationResult<T> Execute<T>(IOperation<T> operation);
        public abstract IOperationResult Execute(IOperation operation);
        public abstract Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection);
        public abstract Task ExecuteAsync<T>(IOperation<T> operation);
        public abstract Task ExecuteAsync(IOperation operation, IConnection connection);
        public abstract Task ExecuteAsync(IOperation operation);

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
        public virtual IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            //Get the request buffer and send it
            var request = operation.Write();
            var response = connection.Send(request);

            //Read the response and return the completed operation
            operation.Read(response, ErrorMap);
            return operation.GetResultWithValue();
        }
    }
}
