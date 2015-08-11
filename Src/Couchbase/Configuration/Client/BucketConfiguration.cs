using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.IO;
using Couchbase.Utils;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// The configuration setttings for a Bucket.
    /// </summary>
    /// <remarks>The default setting use 127.0.0.1 and port 11210.</remarks>
    public sealed class BucketConfiguration
    {
        private int _observeTimeout;
        private int _observeInterval;
        public const int SslPort = 11207;
        private uint _operationLifespan;
        private bool _operationLifespanChanged;

        /// <summary>
        /// Default CTOR for localhost.
        /// </summary>
        public BucketConfiguration()
        {
            Servers = new List<Uri> { new Uri("http://localhost:8091/pools") };
            Port = 11210;
            Password = string.Empty;
            Username = string.Empty;
            BucketName = "default";
            ObserveInterval = 10; //ms
            ObserveTimeout = 500; //ms
            _operationLifespan = 2500; //ms, work around property that flags as changed
            PoolConfiguration = new PoolConfiguration();
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use enhanced durability if the
        /// Couchbase server version supports it; if it's not supported the client will use
        /// Observe for Endure operations.
        /// </summary>
        /// <value>
        /// <c>true</c> to use enhanced durability; otherwise, <c>false</c>.
        /// </value>
        public bool UseEnhancedDurability
        {
            get { return PoolConfiguration.UseEnhancedDurability; }
            set { PoolConfiguration.UseEnhancedDurability = value; }
        }

        /// <summary>
        /// Set to true to enable Secure Socket Layer (SSL) encryption of all traffic between the client and the server.
        /// </summary>
        public bool UseSsl { get; set; }

        /// <summary>
        /// A list of IP's to bootstrap off of.
        /// </summary>
        public List<Uri> Servers { get; set; }

        /// <summary>
        /// The Memcached port to use.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// The name of the Bucket to connect to.
        /// </summary>
        public string BucketName { get; set; }

        /// <summary>
        /// The password to use if it's a SASL authenticated Bucket.
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// The username for connecting to a Bucket.
        /// </summary>
        /// <remarks>The <see cref="BucketName"/> is used for as the username for connecting to Buckets.</remarks>
        public string Username { get; set; }

        /// <summary>
        /// The <see cref="PoolConfiguration"/> used to create the <see cref="IConnectionPool"/>.
        /// </summary>
        public PoolConfiguration PoolConfiguration { get; set; }

        /// <summary>
        /// Gets or Sets the max time an observe operation will take before timing out.
        /// </summary>
        public int ObserveTimeout
        {
            get { return _observeTimeout; }
            set
            {
                if (value < 1)
                {
                    const string msg = "must be greater than or equal to 1ms.";
                    throw new ArgumentOutOfRangeException("value", msg);
                }
                _observeTimeout = value;
            }
        }

        /// <summary>
        /// Gets or Sets the interval between each observe attempt.
        /// </summary>
        public int ObserveInterval
        {
            get { return _observeInterval; }
            set
            {
                if (value < 0)
                {
                    const string msg = "must be greater than or equal to 0ms.";
                    throw new ArgumentOutOfRangeException("value", msg);
                }
                _observeInterval = value;
            }
        }

        /// <summary>
        /// The maximum time allowed for an operation to live, in milliseconds, for this specific bucket.
        /// <remarks>Default value is 2500 (2.5 seconds)</remarks>
        /// </summary>
        public uint DefaultOperationLifespan {
            get { return _operationLifespan; }
            set
            {
                _operationLifespan = value;
                _operationLifespanChanged = true;
            }
         }


        /// <summary>
        /// Conditionally change the DefaultOperationLifespan property value, if and only if it wasn't already changed
        /// from its default value.
        /// <remarks>Calling this method doesn't count as a changed from default value. That is, calling it twice will return true both times.</remarks>
        /// </summary>
        /// <param name="newDefault">The new value to be affected to DefaultOperationLifespan if it hasn't been changed since construction.</param>
        /// <returns>true if the value was applied, false otherwise (denoting that a custom value had already been applied)</returns>
        public bool UpdateOperationLifespanDefault(uint newDefault)
        {
            if (!_operationLifespanChanged)
            {
                _operationLifespan = newDefault;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a random <see cref="IPEndPoint"/> from the Servers list.
        /// </summary>
        /// <returns></returns>
        public IPEndPoint GetEndPoint()
        {
            var server = Servers.Shuffle().FirstOrDefault();
            if (server == null)
            {
                throw new ArgumentException("server");
            }
            var port = UseSsl ? SslPort : Port;
            return server.GetIPEndPoint(port);
        }

        public List<IPEndPoint> GetEndPoints()
        {
            var endPoints = new List<IPEndPoint>();
            foreach (var server in Servers.Shuffle())
            {
                var port = UseSsl ? SslPort : Port;
                var endPoint = server.GetIPEndPoint(port);
                endPoints.Add(endPoint);
            }
            return endPoints;
        }
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
