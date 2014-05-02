using System;
using System.Configuration;
using System.Net.Sockets;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// Represents a object used to construct the internal <see cref="IConnectionPool"/> object for a <see cref="IBucket"/> instance.
    /// </summary>
    /// <remarks>Default configuration:
    /// MaxSize = 2;
    /// MinSize = 1;
    /// WaitTimeout = 2500;
    /// ReceiveTimeout = 2500;
    /// ShutdownTimeout = 10000;
    /// SendTimeout = 2500;
    /// </remarks>
    public sealed class PoolConfiguration : ConfigurationElement
    {
        public PoolConfiguration()
        {
            MaxSize = 2;
            MinSize = 1;
            WaitTimeout = 2500;
            RecieveTimeout = 2500;
            ShutdownTimeout = 10000;
            SendTimeout = 2500;
        }

        public PoolConfiguration(int maxSize , int minSize, int waitTimeout, int receiveTimeout, int shutdownTimeout,
            int sendTimeout)
        {
            //todo enable app.configuration
            MaxSize = maxSize;
            MinSize = minSize;
            WaitTimeout = waitTimeout;
            RecieveTimeout = receiveTimeout;
            ShutdownTimeout = shutdownTimeout;
            SendTimeout = sendTimeout;
        }

        /// <summary>
        /// The maximum number of connections to create.
        /// </summary>
        public int MaxSize { get; set; }

        /// <summary>
        /// The minimum number of connection to create.
        /// </summary>
        public int MinSize { get; set; }

        /// <summary>
        /// The amount of time a thread will wait for a <see cref="IConnection"/> once the MaxSize of the pool has been reached.
        /// </summary>
        public int WaitTimeout { get; set; }

        [Obsolete]
        public int RecieveTimeout { get; set; }

        /// <summary>
        /// Wait up to the <see cref="ShutdownTimeout"/> to send or recieve data before closing the <see cref="IConnection"/>.
        /// </summary>
        public int ShutdownTimeout { get; set; }


        [Obsolete]
        public int SendTimeout { get; set; }
    }
}