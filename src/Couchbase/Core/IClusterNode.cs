using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Management.Buckets;

namespace Couchbase.Core
{
    internal interface IClusterNode : IDisposable
    {
        IBucket Owner { get; set; }
        NodeAdapter NodesAdapter { get; set; }
        HostEndpoint BootstrapEndpoint { get; }
        IPEndPoint EndPoint { get; }
        BucketType BucketType { get; }

        /// <summary>
        /// Endpoints by which this node may be referenced for key/value operations.
        /// </summary>
        /// <remarks>
        /// May change over time depending on bootstrap status.
        /// </remarks>
        IReadOnlyCollection<IPEndPoint> KeyEndPoints { get; }

        Uri QueryUri { get; set; }
        Uri AnalyticsUri { get; set; }
        Uri SearchUri { get; set; }
        Uri ViewsUri { get; set; }
        Uri ManagementUri { get; set; }
        ErrorMap ErrorMap { get; set; }
        ServerFeatureSet ServerFeatures { get; }
        IConnectionPool ConnectionPool { get; }
        List<Exception> Exceptions { get; set; } //TODO catch and hold until first operation per RFC
        bool IsAssigned { get; }
        bool HasViews { get; }
        bool HasAnalytics { get; }
        bool HasQuery { get; }
        bool HasSearch { get; }
        bool HasKv { get; }
        DateTime? LastViewActivity { get; }
        DateTime? LastQueryActivity { get; }
        DateTime? LastSearchActivity { get; }
        DateTime? LastAnalyticsActivity { get; }
        DateTime? LastKvActivity { get; }
        Task<Manifest> GetManifest();

        /// <summary>
        /// Selects the <see cref="IBucket"/> this <see cref="ClusterNode" /> is associated to.
        /// </summary>
        /// <param name="bucket">The <see cref="IBucket"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task SelectBucketAsync(IBucket bucket, CancellationToken cancellationToken = default);

        Task<BucketConfig> GetClusterMap();
        Task<uint?> GetCid(string fullyQualifiedName);

        Task ExecuteOp(IOperation op, CancellationTokenPair tokenPair = default);

        Task ExecuteOp(IConnection connection, IOperation op, CancellationTokenPair tokenPair = default);

        Task SendAsync(IOperation op, CancellationTokenPair tokenPair = default);

        /// <summary>
        /// Notifies when the <see cref="KeyEndPoints"/> collection is changed.
        /// </summary>
        event NotifyCollectionChangedEventHandler KeyEndPointsChanged;
    }
}
