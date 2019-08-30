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
    internal class ClusterNode : IClusterNode
    {
        public IBucket Owner { get; set; }
        public ClusterOptions ClusterOptions { get; set; }
        public NodeAdapter NodesAdapter { get; set; }
        public Uri BootstrapUri { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public Uri QueryUri { get; set; }
        public Uri AnalyticsUri { get; set; }
        public Uri SearchUri { get; set; }
        public Uri ViewsUri { get; set; }
        public Uri ManagementUri { get; set; }
        public ErrorMap ErrorMap { get; set; }
        public short[] ServerFeatures { get; set; }
        public IConnection Connection { get; set; }//TODO this will be a connection pool later
        public List<Exception> Exceptions { get; set; }//TODO catch and hold until first operation per RFC
        public bool HasViews() => ViewsUri != null && ViewsUri.Port != 0;
        public bool HasAnalytics() => AnalyticsUri != null && AnalyticsUri.Port != 0;
        public bool HasQuery() => QueryUri != null && QueryUri.Port != 0;
        public bool HasSearch() => SearchUri != null && SearchUri.Port != 0;

        public bool Supports(ServerFeatures feature)
        {
            return ServerFeatures.Contains((short) feature);
        }

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
            return Connection.GetClusterMap(EndPoint, BootstrapUri);
        }

        public void BuildServiceUris()
        {
            if (NodesAdapter != null)
            {
                QueryUri = EndPoint.GetQueryUri(ClusterOptions, NodesAdapter);
                SearchUri = EndPoint.GetSearchUri(ClusterOptions, NodesAdapter);
                AnalyticsUri = EndPoint.GetAnalyticsUri(ClusterOptions, NodesAdapter);
                ViewsUri = EndPoint.GetViewsUri(ClusterOptions, NodesAdapter); //TODO move to IBucket level?
                ManagementUri = EndPoint.GetManagementUri(ClusterOptions, NodesAdapter);
            }
        }

        public void Dispose()
        {
            Connection?.Dispose();
        }
    }
}
