using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Operations;

namespace Couchbase.IO
{
    internal interface IOStrategy : IDisposable
    {
        void RegisterListener(IConfigListener listener);

        void UnRegisterListener(IConfigListener listener);

        Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation);

        IOperationResult<T> Execute<T>(IOperation<T> operation);

        IPEndPoint EndPoint { get; }

        IConnectionPool ConnectionPool { get; }
    }
}
