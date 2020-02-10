using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;

namespace Couchbase.Core
{
    internal interface IClusterNode : IDisposable
    {
        IBucket Owner { get; }
        NodeAdapter NodesAdapter { get; set; }
        Uri BootstrapUri { get; set; }
        IPEndPoint EndPoint { get; }
        Uri QueryUri { get; set; }
        Uri AnalyticsUri { get; set; }
        Uri SearchUri { get; set; }
        Uri ViewsUri { get; set; }
        Uri ManagementUri { get; set; }
        ErrorMap ErrorMap { get; set; }
        short[] ServerFeatures { get; set; }
        IConnectionPool ConnectionPool { get; }
        List<Exception> Exceptions { get; set; } //TODO catch and hold until first operation per RFC
        bool IsAssigned { get; }
        bool HasViews { get; }
        bool HasAnalytics { get; }
        bool HasQuery { get; }
        bool HasSearch { get; }
        bool HasKv { get; }
        bool Supports(ServerFeatures feature);
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

        void BuildServiceUris();

        Task ExecuteOp(IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null);

        Task ExecuteOp(IConnection connection, IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null);

        Task SendAsync(IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null);
    }
}
