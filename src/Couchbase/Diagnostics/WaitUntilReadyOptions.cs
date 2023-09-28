using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.Diagnostics
{
    /// <summary>
    /// Optional arguments for the WaitUntilReady methods.
    /// </summary>
    public sealed class WaitUntilReadyOptions
    {
        /// <summary>
        /// This list serves as the default, but a parameter-less call to WaitUntilReady should use the services from the
        /// cluster config instead.
        /// </summary>
        private static readonly IList<ServiceType> DefaultServiceTypes = new List<ServiceType>
        {
            ServiceType.Analytics,
            ServiceType.KeyValue,
            ServiceType.Query,
            ServiceType.Search,
            ServiceType.Views
        };

        /// <summary>
        /// The desired state - the default is Online.
        /// </summary>
        internal ClusterState DesiredStateValue { get; set; } = ClusterState.Online;

        /// <summary>
        /// The service types to check, if not provided all service types reported by the cluster will be checked.
        /// </summary>
        internal IList<ServiceType> ExplicitServiceTypes { get; set; }

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
            ExplicitServiceTypes = serviceTypes;
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

        internal IEnumerable<ServiceType> EffectiveServiceTypes(ClusterContext context)
        {
            // if the user specified specific service types, use those
            if (ExplicitServiceTypes is { Count: > 0 })
            {
                return ExplicitServiceTypes;
            }

            // otherwise, try to discover the service types from the cluster
            if (context?.Nodes is not null)
            {
                ISet<ServiceType> serviceTypes = new SortedSet<ServiceType>();
                foreach (var clusterNode in context.Nodes)
                {
                    foreach (ServiceType serviceType in Enum.GetValues(typeof(ServiceType)))
                    {
                        bool includeService = serviceType switch
                        {
                            ServiceType.Analytics => clusterNode.HasAnalytics,
                            ServiceType.Eventing => clusterNode.HasEventing,
                            ServiceType.KeyValue => clusterNode.HasKv,
                            ServiceType.Query => clusterNode.HasQuery,
                            ServiceType.Search => clusterNode.HasSearch,
                            ServiceType.Views => clusterNode.HasViews,
                            ServiceType.Config => false,
                            ServiceType.Management => false,
                            _ => false,
                        };

                        if (includeService)
                        {
                            serviceTypes.Add(serviceType);
                        }
                    }

                }

                if (serviceTypes.Count > 0)
                {
                    return serviceTypes;
                }
            }

            // if there are no service types discovered, try the default list.
            return DefaultServiceTypes;
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
