using System;

namespace Couchbase.Configuration.Server.Providers
{
    /// <summary>
    /// Types of configuration providers which can be used to bootstrap the cluster
    /// and monitor for cluster changes.
    /// </summary>
    [Flags]
    public enum ServerConfigurationProviders
    {
        None = 0,

        /// <summary>
        /// Binary protocol for streaming cluster information.
        /// </summary>
        CarrierPublication = 1,

        /// <summary>
        /// Uses HTTP RESTful API calls to get cluster information.
        /// </summary>
        HttpStreaming = 2
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
