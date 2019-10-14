using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;

namespace Couchbase.Core
{
    internal interface IClusterNode : IDisposable
    {
        IBucket Owner { get; set; }
        NodeAdapter NodesAdapter { get; set; }
        Uri BootstrapUri { get; set; }
        IPEndPoint EndPoint { get; set; }
        Uri QueryUri { get; set; }
        Uri AnalyticsUri { get; set; }
        Uri SearchUri { get; set; }
        Uri ViewsUri { get; set; }
        Uri ManagementUri { get; set; }
        ErrorMap ErrorMap { get; set; }
        short[] ServerFeatures { get; set; }
        IConnection Connection { get; set; } //TODO this will be a connection pool later
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
        Task SelectBucket(string name);
        Task<BucketConfig> GetClusterMap();
        void BuildServiceUris();

        Task ExecuteOp(IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null);

        Task ExecuteOp(IConnection connection, IOperation op, CancellationToken token = default(CancellationToken),
            TimeSpan? timeout = null);
    }
}
