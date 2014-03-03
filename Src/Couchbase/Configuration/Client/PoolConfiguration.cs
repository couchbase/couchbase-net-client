using System;
using System.Configuration;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Configuration.Client
{
    public sealed class PoolConfiguration : ConfigurationElement
    {
        private int _maxSize;
        private int _minSize;
        private int _receiveTimeout;
        private int _sendTimeout;
        private int _shutdownTimeout;
        private int _waitTimeout;

        public PoolConfiguration()
        {
            _maxSize = 2;
            _minSize = 1;
            _waitTimeout = 2500;
            _receiveTimeout = 2500;
            _shutdownTimeout = 10000;
            _sendTimeout = 2500;
        }

        public PoolConfiguration(int maxSize , int minSize, int waitTimeout, int receiveTimeout, int shutdownTimeout,
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
            set { _maxSize = value; }
        }

        public int MinSize
        {
            get { return _minSize; }
            set { _minSize = value; }
        }

        public int WaitTimeout
        {
            get { return _waitTimeout; }
            set { _waitTimeout = value; }
        }

        public int RecieveTimeout
        {
            get { return _receiveTimeout; }
            set { _receiveTimeout = value; }
        }

        public int ShutdownTimeout
        {
            get { return _shutdownTimeout; }
            set { _shutdownTimeout = value; }
        }

        public int SendTimeout
        {
            get { return _sendTimeout; }
            set { _shutdownTimeout = value; }
        }
    }
}