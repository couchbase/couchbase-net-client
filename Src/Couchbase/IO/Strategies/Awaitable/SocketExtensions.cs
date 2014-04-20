// http://blogs.msdn.com/b/pfxteam/archive/2011/12/15/10248293.aspx
using System.Net.Sockets;

namespace Couchbase.IO.Strategies.Awaitable
{
    public static class SocketExtensions
    {
        public static SocketAwaitable ReceiveAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.ReceiveAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        public static SocketAwaitable SendAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.SendAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        public static SocketAwaitable ConnectAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.ConnectAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        public static SocketAwaitable DisconnectAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.DisconnectAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

        public static SocketAwaitable AcceptAsync(this Socket socket, SocketAwaitable awaitable)
        {
            awaitable.Reset();
            if (!socket.AcceptAsync(awaitable.EventArgs))
            {
                awaitable.IsCompleted = true;
            }
            return awaitable;
        }

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
