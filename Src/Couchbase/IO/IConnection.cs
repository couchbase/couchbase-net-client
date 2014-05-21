using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a TCP connection to a Couchbase Server instance.
    /// </summary>
    internal interface IConnection : IDisposable
    {
        /// <summary>
        /// The Socket used for IO.
        /// </summary>
        Socket Socket { get; }

        /// <summary>
        /// Unique identifier for this connection.
        /// </summary>
        Guid Identity { get; }

        /// <summary>
        /// True if the connection has been SASL authenticated.
        /// </summary>
        bool IsAuthenticated { get; set; }

        void Send(byte[] buffer, int offset, int length, OperationAsyncState state);

        void Receive(byte[] buffer, int offset, int length, OperationAsyncState state);

        OperationAsyncState State { get; set; }
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