using System;
using System.Collections.Generic;
using System.Configuration;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Strategies;

namespace Couchbase.Tests.Utils
{
    public static class ClientConfigUtil
    {
        /// <summary>
        /// Gets the connection using the value of the <c>bootstrapUrl</c> in the App.Config as the node to bootstrap from.
        /// </summary>
        /// <returns></returns>
        public static ClientConfiguration GetConfiguration()
        {
            var config = new ClientConfiguration
            {
                Servers = new List<Uri>
                {
                    new Uri(ConfigurationManager.AppSettings["bootstrapUrl"])
                }
            };
            config.ConnectionPoolCreator = ConnectionPoolFactory.GetFactory<ConnectionPool<MultiplexedConnection>>();
            config.IOServiceCreator = IOStrategyFactory.GetFactory<MultiplexedIOStrategy>();
            config.Initialize();
            return config;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
