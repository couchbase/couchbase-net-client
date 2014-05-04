using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Operations;

namespace Couchbase.IO
{
    /// <summary>
    /// Primary interface for the IO engine.
    /// </summary>
    internal interface IOStrategy : IDisposable
    {
        /// <summary>
        /// Executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="connection"></param>
        /// <returns></returns>
        IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection);

        IOperationResult<T> Execute<T>(IOperation<T> operation);

        IPEndPoint EndPoint { get; }

        IConnectionPool ConnectionPool { get; }

        ISaslMechanism SaslMechanism { set; }
    }
}
