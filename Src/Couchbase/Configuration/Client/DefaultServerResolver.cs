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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
