// http://blogs.msdn.com/b/pfxteam/archive/2011/12/15/10248293.aspx
using System.Net.Sockets;

namespace Couchbase.IO.Strategies.Awaitable
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
