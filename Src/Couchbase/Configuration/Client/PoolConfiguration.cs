﻿using System;
using System.Configuration;
using Common.Logging;
using Couchbase.Core;
using Couchbase.IO;
using Newtonsoft.Json;

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
    /// OperationTimeout = 2500;
    /// MaxConnectionAcquireCount = 5;
    /// ConnectionTimeout = 15000;
    /// </remarks>
    public sealed class PoolConfiguration : ConfigurationElement
    {
        public PoolConfiguration(ClientConfiguration clientConfiguration = null)
        {
            MaxSize = 2;
            MinSize = 1;
            WaitTimeout = 2500;
            RecieveTimeout = 2500;
            ShutdownTimeout = 10000;
            OperationTimeout = 2500;
            MaxAcquireIterationCount = 5;
            ConnectionTimeout = 15000;
            BufferSize = 1024 * 16;

            //in some cases this is needed all the way down the stack
            ClientConfiguration = clientConfiguration;
            BufferAllocator = (p) => new BufferAllocator(p.MaxSize * p.BufferSize, p.BufferSize);
        }

        public PoolConfiguration(int maxSize, int minSize, int waitTimeout, int receiveTimeout, int shutdownTimeout,
            int operationTimeout, int maxAcquireIterationCount, ClientConfiguration clientConfiguration = null)
        {
            //todo enable app.configuration
            MaxSize = maxSize;
            MinSize = minSize;
            WaitTimeout = waitTimeout;
            RecieveTimeout = receiveTimeout;
            ShutdownTimeout = shutdownTimeout;
            OperationTimeout = operationTimeout;
            MaxAcquireIterationCount = maxAcquireIterationCount;
            ClientConfiguration = clientConfiguration;
            BufferAllocator = (p) => new BufferAllocator(p.MaxSize * p.BufferSize, p.BufferSize);
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

        /// <summary>
        /// The maximum number of iterations that a thread will wait for an available connection before throwing a <see cref="ConnectionUnavailableException"/>.
        /// </summary>
        /// <remarks>The default is 5 iterations.</remarks>
        public int MaxAcquireIterationCount { get; set; }

        [Obsolete]
        public int RecieveTimeout { get; set; }

        /// <summary>
        /// Wait up to the <see cref="ShutdownTimeout"/> to send or recieve data before closing the <see cref="IConnection"/>.
        /// </summary>
        public int ShutdownTimeout { get; set; }

        /// <summary>
        /// The amount of time to wait for a pending operation to complete before timing out.
        /// </summary>
        /// <remarks>Default is 2500ms</remarks>
        /// <remarks>Operations exceeding this timeout will return the following message: "Timed out"</remarks>
        public int OperationTimeout { get; set; }

        /// <summary>
        /// Set to true to enable Secure Socket Layer (SSL) encryption of all traffic between the client and the server.
        /// </summary>
        public bool UseSsl { get; set; }

        /// <summary>
        /// Cancels a pending operation if it does not complete in the time given and marks the connection as dead.
        /// </summary>
        public int ConnectionTimeout { get; set; }

        /// <summary>
        /// References the top level <see cref="ClientConfiguration"/> object.
        /// </summary>
        [JsonIgnore]
        public ClientConfiguration ClientConfiguration { get; set; }

        /// <summary>
        /// Writes the elasped time for an operation to the log appender Disabled by default.
        /// </summary>
        /// <remarks>When enabled will cause severe performance degradation.</remarks>
        /// <remarks>Requires a <see cref="LogLevel"/>of DEBUG to be enabled as well.</remarks>
        public bool EnableOperationTiming
        {
            get
            {
                var enabled = false;
                if (ClientConfiguration != null)
                {
                    enabled = ClientConfiguration.EnableOperationTiming;
                }
                return enabled;
            }
        }

        /// <summary>
        /// The size of each buffer to allocate per TCP connection for sending and recieving Memcached operations
        /// </summary>
        /// <remarks>The default is 16K</remarks>
        /// <remarks>The total buffer size is BufferSize * PoolConfiguration.MaxSize</remarks>
        public int BufferSize { get; set; }

        [JsonIgnore]
        internal Func<PoolConfiguration, BufferAllocator> BufferAllocator { get; set; }
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
