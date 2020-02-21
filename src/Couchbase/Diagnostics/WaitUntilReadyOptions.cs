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
