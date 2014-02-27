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
                var handle = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = config.RecieveTimeout,
                    SendTimeout = config.SendTimeout,
                };
                handle.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, config.RecieveTimeout);
                handle.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, config.SendTimeout);
                handle.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                handle.Connect(p.EndPoint);
                return new DefaultConnection(p, handle);
            };
            return factory;
        }
    }
}
