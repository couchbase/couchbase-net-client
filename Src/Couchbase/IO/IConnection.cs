using System;
using System.Net.Sockets;

namespace Couchbase.IO
{
    internal interface IConnection : IDisposable
    {
        Socket Socket { get; }

        Guid Identity { get; }
    }
}
