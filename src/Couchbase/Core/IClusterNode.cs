using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.Legacy.Errors;

namespace Couchbase.Core
{
    internal interface IClusterNode : IDisposable
    {
        IBucket Owner { get; set; }
        Couchbase.Configuration Configuration { get; set; }
        NodeAdapter NodesAdapter { get; set; }
        Uri BootstrapUri { get; set; }
        IPEndPoint EndPoint { get; set; }
        Uri QueryUri { get; set; }
        Uri AnalyticsUri { get; set; }
        Uri SearchUri { get; set; }
        Uri ViewsUri { get; set; }
        ErrorMap ErrorMap { get; set; }
        short[] ServerFeatures { get; set; }
        IConnection Connection { get; set; } //TODO this will be a connection pool later
        List<Exception> Exceptions { get; set; } //TODO catch and hold until first operation per RFC
        bool HasViews();
        bool HasAnalytics();
        bool HasQuery();
        bool HasSearch();
        bool Supports(ServerFeatures feature);
        Task<Manifest> GetManifest();
        Task SelectBucket(string name);
        Task<BucketConfig> GetClusterMap();
        void BuildServiceUris();
    }
}
