using System;
using System.Net;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;

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

        IViewResult<T> Send<T>(IViewQuery query);

        IQueryResult<T> Send<T>(string query);

        IViewClient ViewClient { get; }

        IQueryClient QueryClient { get; }
        
        IPEndPoint EndPoint { get; }
        void Authenticate(string username, string password);
    }
}
