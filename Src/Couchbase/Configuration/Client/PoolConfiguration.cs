using System;
using System.Configuration;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Configuration.Client
{
    public sealed class PoolConfiguration : ConfigurationElement
    {
        private readonly int _maxSize;
        private readonly int _minSize;
        private readonly int _receiveTimeout;
        private readonly int _sendTimeout;
        private readonly int _shutdownTimeout;
        private readonly int _waitTimeout;

        public PoolConfiguration()
        {
            _maxSize = 2;
            _minSize = 1;
            _waitTimeout = 2500;
            _receiveTimeout = 2500;
            _shutdownTimeout = 10000;
            _sendTimeout = 2500;
        }

        public PoolConfiguration(int maxSize, int minSize, int waitTimeout, int receiveTimeout, int shutdownTimeout,
            int sendTimeout)
        {
            //todo enable app.configuration
            _maxSize = maxSize;
            _minSize = minSize;
            _waitTimeout = waitTimeout;
            _receiveTimeout = receiveTimeout;
            _shutdownTimeout = shutdownTimeout;
            _sendTimeout = sendTimeout;
        }

        public int MaxSize
        {
            get { return _maxSize; }
        }

        public int MinSize
        {
            get { return _minSize; }
        }

        public int WaitTimeout
        {
            get { return _waitTimeout; }
        }

        public int RecieveTimeout
        {
            get { return _receiveTimeout; }
        }

        public int ShutdownTimeout
        {
            get { return _shutdownTimeout; }
        }

        public int SendTimeout
        {
            get { return _sendTimeout; }
        }
    }
}