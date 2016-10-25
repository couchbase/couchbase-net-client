using System;
using System.Collections.Generic;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// This class represents a default server resolver that returns a seed server of localhost
    /// and port 8091.  In a production environment where configurations are managed outside
    /// of the client's configuration file (app.conf), a real implementation of this class
    /// will need to be created and a reference to that class placed in the client configuration.
    ///
    /// For example, if you wanted to load your server configuration from SRV records, you might
    /// create records that look like this in your DNS:
    ///
    /// <pre>
    /// _cbmcd._tcp.example.com.  0  IN  SRV  20  0  11210 node2.example.com.
    /// _cbmcd._tcp.example.com.  0  IN  SRV  10  0  11210 node1.example.com.
    /// _cbmcd._tcp.example.com.  0  IN  SRV  30  0  11210 node3.example.com.
    ///
    /// _cbhttp._tcp.example.com.  0  IN  SRV  20  0  8091 node2.example.com.
    /// _cbhttp._tcp.example.com.  0  IN  SRV  10  0  8091 node1.example.com.
    /// _cbhttp._tcp.example.com.  0  IN  SRV  30  0  8091 node3.example.com.
    /// </pre>
    ///
    /// In your application configuration file, you would specify the ServerResolver element with
    /// type property set to your custom class. The real implementation class would be able to query
    /// and produce a list of URIs for the Couchbase client to use.
    /// </summary>
    public class DefaultServerResolver : IServerResolver
    {
        public List<Uri> GetServers()
        {
            return new List<Uri>
            {
                new Uri("http://localhost:8081/pools")
            };
        }
    }
}
