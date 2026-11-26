using System;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections.Channels
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolFactory"/>.
    /// </summary>
    internal partial class ChannelConnectionPoolFactory : IConnectionPoolFactory
    {
        private readonly IConnectionFactory _connectionFactory;
        private readonly ClusterOptions _clusterOptions;
        private readonly IConnectionPoolScaleControllerFactory _scaleControllerFactory;
        private readonly Redactor _redactor;
        private readonly ILogger<ChannelConnectionPool> _channelPoolLogger;
        private readonly TimeProvider _timeProvider;

        public ChannelConnectionPoolFactory(IConnectionFactory connectionFactory, ClusterOptions clusterOptions,
            IConnectionPoolScaleControllerFactory scaleControllerFactory, Redactor redactor,
            ILogger<ChannelConnectionPool> channelPoolLogger, TimeProvider timeProvider)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _scaleControllerFactory = scaleControllerFactory ?? throw new ArgumentNullException(nameof(scaleControllerFactory));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _channelPoolLogger = channelPoolLogger ?? throw new ArgumentNullException(nameof(channelPoolLogger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <inheritdoc />
        /// <remarks>
        /// A cluster node gets one connection, as RFC 48 asks. It never gets <see cref="SingleConnectionPool"/>, because
        /// that pool does not retry a failed first connect.
        /// </remarks>
        public IConnectionPool Create(ClusterNode clusterNode)
        {
            var bucketName = clusterNode.FixedBucketName;

            if (bucketName is null)
            {
                return CreateChannelPool(clusterNode, bucketName, minimumSize: 1, maximumSize: 1);
            }

            if (_clusterOptions.NumKvConnections <= 1 && _clusterOptions.MaxKvConnections <= 1)
            {
                LogUsingConnectionPool("Single");
                return new SingleConnectionPool(clusterNode, _connectionFactory, _channelPoolLogger, bucketName);
            }

            return CreateChannelPool(clusterNode, bucketName, _clusterOptions.NumKvConnections,
                _clusterOptions.MaxKvConnections);
        }

        private ChannelConnectionPool CreateChannelPool(ClusterNode clusterNode, string? bucketName, int minimumSize,
            int maximumSize)
        {
            LogUsingConnectionPool("Channel");

            var scaleController = _scaleControllerFactory.Create();

            return new ChannelConnectionPool(clusterNode, _connectionFactory, scaleController,
                _redactor, _channelPoolLogger, _timeProvider, (int) _clusterOptions.KvSendQueueCapacity, bucketName)
            {
                MinimumSize = minimumSize,
                MaximumSize = maximumSize
            };
        }

        #region Logging

        [LoggerMessage(LoggingEvents.ChannelConnectionEvent, LogLevel.Information, "Using the {type}ConnectionPool.")]
        private partial void LogUsingConnectionPool(string type);

        #endregion

    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
