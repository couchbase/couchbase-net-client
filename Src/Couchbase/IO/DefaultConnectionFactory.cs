using System;
using System.Net.Sockets;

namespace Couchbase.IO
{
    public static class DefaultConnectionFactory
    {
        internal static Func<IConnectionPool, IConnection> GetDefault()
        {
            Func<IConnectionPool, IConnection> factory = p =>
            {
                var config = p.Configuration;
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = config.RecieveTimeout,
                    SendTimeout = config.SendTimeout,
                };
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, config.RecieveTimeout);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, config.SendTimeout);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.Connect(p.EndPoint);
                return new DefaultConnection(p, socket);
            };
            return factory;
        }
    }
}
