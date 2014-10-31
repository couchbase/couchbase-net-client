using System;
using System.Net;
using System.Security.Authentication;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Strategies
{
// ReSharper disable once InconsistentNaming
    internal class DefaultIOStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;

        private volatile bool _disposed;
        private ISaslMechanism _saslMechanism;
        private readonly Guid _identity = Guid.NewGuid();

        public DefaultIOStrategy(IConnectionPool connectionPool)
        {
            Log.Debug(m=>m("Creating DefaultIOStrategy {0}", _identity));
            _connectionPool = connectionPool;
        }

        public DefaultIOStrategy(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
        {
            Log.Debug(m => m("Creating DefaultIOStrategy {0}", _identity));
            _connectionPool = connectionPool;
            _saslMechanism = saslMechanism;
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
                    Log.Debug(m => m("Authenticated {0} using {1} - {2}.", _saslMechanism.Username, _saslMechanism.GetType(), _identity));
                    connection.IsAuthenticated = true;
                }
                else
                {
                    Log.Debug(m => m("Could not authenticate {0} using {1} - {2}.", _saslMechanism.Username, _saslMechanism.GetType(), _identity));
                    throw new AuthenticationException(_saslMechanism.Username);
                }
            }
        }

        /// <summary>
        /// Returns true if internal TCP connections are using SSL.
        /// </summary>
        public bool IsSecure
        {
            get
            {
                var connection = _connectionPool.Acquire();
                var isSecure = connection.IsSecure;
                _connectionPool.Release(connection);
                return isSecure;
            }
        }

        public void Dispose()
        {
            Log.Debug(m => m("Disposing DefaultIOStrategy for {0} - {1}", EndPoint, _identity));
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
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
