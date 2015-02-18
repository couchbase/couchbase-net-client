using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;

namespace Couchbase.IO
{
    /// <summary>
    /// Primary interface for the IO engine.
    /// </summary>
    internal interface IOStrategy : IDisposable
    {
        /// <summary>
        /// Executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> being executed.</param>
        /// <param name="connection">The <see cref="IConnection"/> the operation is using.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> representing the result of operation.</returns>
        /// <remarks>This overload is used to perform authentication on the connection if it has not already been authenticated.</remarks>
        IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection);

        /// <summary>
        /// Executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> being executed.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> representing the result of operation.</returns>
        IOperationResult<T> Execute<T>(IOperation<T> operation);

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> being executed.</param>
        /// <param name="connection">The <see cref="IConnection"/> the operation is using.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> representing the result of operation.</returns>
        /// <remarks>This overload is used to perform authentication on the connection if it has not already been authenticated.</remarks>
        Task ExecuteAsync<T>(IOperation<T> operation, IConnection connection);

        /// <summary>
        /// Asynchrounously executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T">The Type T of the value being stored or retrieved.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> being executed.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> representing the result of operation.</returns>
        /// <remarks>This overload is used to perform authentication on the connection if it has not already been authenticated.</remarks>
        Task ExecuteAsync<T>(IOperation<T> operation);

        /// <summary>
        /// The IP endpoint of the node in the cluster that this <see cref="IOStrategy"/> instance is communicating with.
        /// </summary>
        IPEndPoint EndPoint { get; }

        /// <summary>
        /// The <see cref="IConnectionPool"/> that this <see cref="IOStrategy"/> instance is using for acquiring <see cref="IConnection"/>s.
        /// </summary>
        IConnectionPool ConnectionPool { get; }

        /// <summary>
        /// The SASL mechanism type the <see cref="IOStrategy"/> is using for authentication.
        /// </summary>
        /// <remarks>This could be PLAIN or CRAM-MD5 depending upon what the server supports.</remarks>
        ISaslMechanism SaslMechanism { set; }

        /// <summary>
        /// Returns true if internal TCP connections are using SSL.
        /// </summary>
        bool IsSecure { get; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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