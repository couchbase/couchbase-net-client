using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies
{
// ReSharper disable once InconsistentNaming
    internal class DefaultIOStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private volatile bool _disposed;
        private ISaslMechanism _saslMechanism;

        public DefaultIOStrategy(IConnectionPool connectionPool)
            : this(connectionPool,
            new PlainTextMechanism("default", string.Empty))
        {
        }

        public DefaultIOStrategy(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
        {
            _connectionPool = connectionPool;
            _saslMechanism = saslMechanism;
            _saslMechanism.IOStrategy = this;
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            return connection.Send(operation);
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            var connection = _connectionPool.Acquire();
            if (!connection.IsAuthenticated)
            {
               Authenticate(connection);
            }
            var result = Execute(operation, connection);
            _connectionPool.Release(connection);
            return result;
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public ISaslMechanism SaslMechanism
        {
            set { _saslMechanism = value; }
        }

        private void Authenticate(IConnection connection)
        {
            if (_saslMechanism != null)
            {
                var result = _saslMechanism.Authenticate(connection);
                if (result)
                {
                    Log.Debug(m => m("Authenticated {0} using {1}.", _saslMechanism.Username, _saslMechanism.GetType()));
                    connection.IsAuthenticated = true;
                }
                else
                {
                    Log.Debug(m => m("Could not authenticate {0} using {1}.", _saslMechanism.Username, _saslMechanism.GetType()));
                    throw new AuthenticationException(_saslMechanism.Username);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                if (_connectionPool != null)
                {
                    _connectionPool.Dispose();
                }
            }
            _disposed = true;
        }

        ~DefaultIOStrategy()
        {
            Dispose(false);
        }
    }
}
