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

        /// <summary>
        /// Set to true to enable Secure Socket Layer (SSL) encryption of all traffic between the client and the server.
        /// </summary>
        public bool EncryptTraffic { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
