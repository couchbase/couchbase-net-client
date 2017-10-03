namespace Couchbase.Configuration
{
    /// <summary>
    /// Represents the "default" ports that come pre-configured with Couchbase Server.
    /// </summary>
    public enum DefaultPorts
    {
        /// <summary>
        /// The Managment REST API port.
        /// </summary>
        MgmtApi = 8091,

        /// <summary>
        /// The Views REST API port.
        /// </summary>
        CApi = 8092,

        /// <summary>
        /// The port used for Binary Memcached TCP operations.
        /// </summary>
        Direct = 11210,

        /// <summary>
        /// Not used by the .NET client - reserved for Moxi.
        /// </summary>
        Proxy = 11211,

        /// <summary>
        /// The SSL port used for Binary Memcached TCP operations.
        /// </summary>
        SslDirect = 11207,

        /// <summary>
        /// The SSL port used by View REST API.
        /// </summary>
        HttpsCApi = 18092,

        /// <summary>
        /// The SSL port used by the Managment REST API's.
        /// </summary>
        HttpsMgmt = 18091,

        /// <summary>
        /// The port used to submit analytics (CBAS) operations.
        /// </summary>
        Analytics = 8095,

        /// <summary>
        /// The SSL port used to submit analytics (CBAS) operations.
        /// </summary>
        AnalytucsSsl = 18095
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
