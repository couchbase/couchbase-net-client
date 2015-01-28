﻿// http://blogs.msdn.com/b/pfxteam/archive/2011/12/15/10248293.aspx

using System.Net.Sockets;

namespace Couchbase.IO.Utils
{
    /// <summary>
    /// Extension methods for using <see cref="SocketAwaitable"/> instances for awaitable async IO.
    /// </summary>
    public static class SocketExtensions
    {
        /// <summary>
        /// Begins an asynchronous request to receive data from a connected <see cref="Socket"/> object using await.
        /// </summary>
        /// <param name="socket">The connected <see cref="Socket"/> object to use.</param>
        /// <param name="awaitable">The <see cref="SocketAwaitable"/> to await on.</param>
        /// <returns>A <see cref="SocketAwaitable"/> object ready to be reused.</returns>
        public static SocketAwaitable ReceiveAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.ReceiveAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        /// <summary>
        /// Sends data asynchronously to a connected <see cref="Socket"/> object using await.
        /// </summary>
        /// <param name="socket">The connected <see cref="Socket"/> object to use.</param>
        /// <param name="awaitable">The <see cref="SocketAwaitable"/> to await on.</param>
        /// <returns>A <see cref="SocketAwaitable"/> object ready to be reused.</returns>
        public static SocketAwaitable SendAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.SendAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        /// <summary>
        /// Begins an asynchronous to a connection to a remote host using await.
        /// </summary>
        /// <param name="socket">The connected <see cref="Socket"/> object to use.</param>
        /// <param name="awaitable">The <see cref="SocketAwaitable"/> to await on.</param>
        /// <returns>A <see cref="SocketAwaitable"/> object ready to be reused.</returns>
        public static SocketAwaitable ConnectAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.ConnectAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        /// <summary>
        /// Begins an asynchronous to disconnect from a remote host using await.
        /// </summary>
        /// <param name="socket">The connected <see cref="Socket"/> object to use.</param>
        /// <param name="awaitable">The <see cref="SocketAwaitable"/> to await on.</param>
        /// <returns>A <see cref="SocketAwaitable"/> object ready to be reused.</returns>
        public static SocketAwaitable DisconnectAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.DisconnectAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        /// <summary>
        /// Begins an asynchronous to accept an incoming connection attempt using await.
        /// </summary>
        /// <param name="socket">The connected <see cref="Socket"/> object to use.</param>
        /// <param name="awaitable">The <see cref="SocketAwaitable"/> to await on.</param>
        /// <returns>A <see cref="SocketAwaitable"/> object ready to be reused.</returns>
        public static SocketAwaitable AcceptAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.AcceptAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        /// <summary>
        /// Begins an asynchronous request to receive data from a connected <see cref="Socket"/> object using await.
        /// </summary>
        /// <param name="awaitable">The <see cref="SocketAwaitable"/> to await on.</param>
        /// <returns>A <see cref="SocketAwaitable"/> object ready to be reused.</returns>
        public static SocketAwaitable ReceiveAsync(this SocketAwaitable awaitable)
        {
            awaitable.Reset();
            var socket = awaitable.EventArgs.AcceptSocket;
            if (!socket.ReceiveAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        /// <summary>
        /// Sends data asynchronously to a connected <see cref="Socket"/> object using await.
        /// </summary>
        /// <param name="awaitable">The <see cref="SocketAwaitable"/> to await on.</param>
        /// <returns>A <see cref="SocketAwaitable"/> object ready to be reused.</returns>
        public static SocketAwaitable SendAsync(this SocketAwaitable awaitable)
        {
            awaitable.Reset();
            var socket = awaitable.EventArgs.AcceptSocket;
            if (!socket.SendAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }
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