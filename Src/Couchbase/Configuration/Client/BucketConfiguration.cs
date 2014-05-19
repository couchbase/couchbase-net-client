using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
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
        public BucketConfiguration()
        {
            Servers = new List<string> {"127.0.0.1" };
            Port = 11210;
            Password = string.Empty;
            Username = string.Empty;
            BucketName = "default";
        }

        /// <summary>
        /// A list of IP's to bootstrap off of.
        /// </summary>
        public List<string> Servers { get; set; }

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
        /// Gets a random <see cref="IPEndPoint"/> from the Servers list.
        /// </summary>
        /// <returns></returns>
        public IPEndPoint GetEndPoint()
        {
            var server = Servers.Shuffle().FirstOrDefault();
            if (server == null)
            {
                throw new ArgumentNullException("server");//change this to a custom exception
            }

            IPAddress ipAddress;
            if (!IPAddress.TryParse(server, out ipAddress))
            {
                throw new ArgumentException("ipAddress");
            }

            return new IPEndPoint(ipAddress, Port);
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
