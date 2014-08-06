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
        /// <summary>
        /// Default CTOR for localhost.
        /// </summary>
        public BucketConfiguration()
        {
            Servers = new List<Uri> {new Uri("http://127.0.0.1") };
            Port = 11210;
            Password = string.Empty;
            Username = string.Empty;
            BucketName = "default";
            ObserveInterval = 10; //ms
            ObserveTimeout = 500; //ms
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
