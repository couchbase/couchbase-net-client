using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Optional arguments for the WaitUntilReady methods.
    /// </summary>
    public sealed class WaitUntilReadyOptions
    {
        /// <summary>
        /// The desired state - the default is Online.
        /// </summary>
        internal ClusterState DesiredStateValue { get; set; } = ClusterState.Online;

        /// <summary>
        /// The service types to check, if not provided all service types will be checked.
        /// </summary>
        internal IList<ServiceType> ServiceTypesValue { get; set; } = new List<ServiceType>
        {
            ServiceType.Analytics,
            ServiceType.KeyValue,
            ServiceType.Query,
            ServiceType.Search,
            ServiceType.Views
        };

        /// <summary>
        /// A cancellation token for cooperative task cancellation.
        /// </summary>
        internal CancellationToken CancellationTokenValue { get; set; }

        /// <summary>
        /// The desired state - the default is Online.
        /// </summary>
        public WaitUntilReadyOptions DesiredState(ClusterState clusterState)
        {
            DesiredStateValue = clusterState;
            return this;
        }

        /// <summary>
        /// The service types to check, if not provided all service types will be checked.
        /// </summary>
        public WaitUntilReadyOptions ServiceTypes(params ServiceType[] serviceTypes)
        {
            ServiceTypesValue = serviceTypes;
            return this;
        }

        /// <summary>
        /// A cancellation token for cooperative task cancellation.
        /// </summary>
        public WaitUntilReadyOptions CancellationToken(CancellationToken cancellationToken)
        {
            CancellationTokenValue = cancellationToken;
            return this;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
