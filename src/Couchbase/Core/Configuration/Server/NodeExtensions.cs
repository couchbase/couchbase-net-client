namespace Couchbase.Core.Configuration.Server
{
    public static class NodeExtensions
    {

        public static bool SupportsKeyValue(this Node node)
        {
            return node.Ports?.Direct > 0 || node.Ports?.SslDirect > 0;
        }

        public static string GetHostName(this Node node)
        {
            return node.Hostname.Contains("$HOST") ? "localhost" : node.Hostname.Split(':')[0];
        }

        public static int GetKeyValuePort(this Node node, Couchbase.Configuration configuration)
        {
            return configuration.UseSsl ? node.Ports.Direct : node.Ports.SslDirect;
        }
    }
}
