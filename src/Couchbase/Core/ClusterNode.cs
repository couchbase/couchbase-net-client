using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Operations.Legacy.Errors;
using Couchbase.Utils;

namespace Couchbase.Core
{
    internal class ClusterNode : IDisposable
    {
        public IBucket Owner { get; set; }

        public Couchbase.Configuration Configuration { get; set; }

        public NodesExt NodesExt { get; set; }

        public Uri BootstrapUri { get; internal set; }

        public IPEndPoint EndPoint { get; internal set; }

        public Uri QueryUri { get; internal set; }

        public Uri AnalyticsUri { get; internal set; }

        public Uri SearchUri { get; internal set; }

        public Uri ViewsUri { get; internal set; }

        public ErrorMap ErrorMap { get; internal set; }

        public short[] ServerFeatures { get; internal set; }

        //TODO this will be a connection pool later
        public IConnection Connection { get; internal set; }

        //TODO catch and hold until first operation per RFC
        public List<Exception> Exceptions { get; internal set; }

        public bool Supports(ServerFeatures feature)
        {
            return ServerFeatures.Contains((short) feature);
        }

        public bool HasViews() => ViewsUri != null && ViewsUri.Port != 0;

        public bool HasAnalytics() => AnalyticsUri != null && AnalyticsUri.Port != 0;

        public bool HasQuery() => QueryUri != null && QueryUri.Port != 0;

        public bool HasSearch() => SearchUri != null && SearchUri.Port != 0;

        //TODO these methods will be more complex once we have a cpool
        public Task<Manifest> GetManifest()
        {
           return Connection.GetManifest();
        }

        public Task SelectBucket(string name)
        {
            return Connection.SelectBucket(name);
        }

        public Task<BucketConfig> GetClusterMap()
        {
            return Connection.GetClusterMap(EndPoint);
        }

        public void BuildServiceUris()
        {
            QueryUri = EndPoint.GetQueryUri(Configuration, NodesExt);
            SearchUri = EndPoint.GetSearchUri(Configuration, NodesExt);
            AnalyticsUri = EndPoint.GetAnalyticsUri(Configuration, NodesExt);
            ViewsUri = EndPoint.GetViewsUri(Configuration, NodesExt);//TODO move to IBucket level?
        }

        public void Dispose()
        {
            Connection?.Dispose();
        }
    }
}
