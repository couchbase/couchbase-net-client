using System;
using System.Net.Sockets;

namespace Couchbase.IO
{
    internal interface IConnection : IDisposable
    {
        Socket Handle { get; }

        Guid Identity { get; }
    }
}
