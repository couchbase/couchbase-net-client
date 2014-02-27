using System;
using System.Net;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase.Core
{
    internal interface IServer : IDisposable
    {
        uint DirectPort { get; }
        
        uint ProxyPort { get; }

        uint Replication { get; }

        bool Active { get; }

        bool Healthy { get; }

        IConnectionPool ConnectionPool { get; }

        IOperationResult<T> Send<T>(IOperation<T> operation);

        IPEndPoint EndPoint { get; }
    }
}
