using System.Net;

namespace Couchbase.Core.Configuration.Server
{
    internal static class NodesExtExtensions
    {
        public static bool SupportsKeyValue(this NodesExt nodesExt)
        {
            return nodesExt.Services?.Kv > 0 || nodesExt.Services?.KvSsl > 0;
        }

        public static bool SupportsSearch(this NodesExt nodesExt)
        {
            return nodesExt.Services?.Fts > 0 || nodesExt.Services?.FtsSsl > 0;
        }

        public static bool SupportsAnalytics(this NodesExt nodesExt)
        {
            return nodesExt.Services?.Cbas > 0 || nodesExt.Services?.CbasSsl > 0;
        }

        public static bool SupportsQuery(this NodesExt nodesExt)
        {
            return nodesExt.Services?.N1Ql > 0 || nodesExt.Services?.N1QlSsl > 0;
        }

        public static string GetHostName(this NodesExt nodesExt)
        {
            //TODO add support using AlternateAddress hostname
            return nodesExt.Hostname.Contains("$HOST") ? "localhost" : nodesExt.Hostname;
        }

        public static int GetKeyValuePort(this NodesExt nodesExt, ClusterOptions clusterOptions)
        {
            return clusterOptions.EnableTls ? nodesExt.Services.KvSsl : nodesExt.Services.Kv;
        }
    }
}
