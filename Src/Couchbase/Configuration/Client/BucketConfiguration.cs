using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Utils;

namespace Couchbase.Configuration.Client
{
    public class BucketConfiguration
    {
        public BucketConfiguration()
        {
            Servers = new List<string> {"127.0.0.1" };
            Port = 11210;
            Password = string.Empty;
            Username = string.Empty;
            BucketName = "default";
            PoolConfiguration = new PoolConfiguration();
        }

        public List<string> Servers { get; set; }

        public int Port { get; set; }

        public string BucketName { get; set; }

        public string Password { get; set; }

        public string Username { get; set; }

        public PoolConfiguration PoolConfiguration { get; set; }

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
