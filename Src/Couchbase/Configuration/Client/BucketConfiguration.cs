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
