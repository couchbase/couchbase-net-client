using System;
using System.ComponentModel;
using System.Net;
using Couchbase.Core.Configuration.Server;
using Newtonsoft.Json;

namespace Couchbase.Utils
{
    internal static class StringExtensions
    {
        //TODO refactor/harden/tests - perhaps move to different class
        public static IPEndPoint GetIpEndPoint(this NodesExt nodesExt, ClusterOptions clusterOptions)
        {
            var useSsl = clusterOptions.EnableTls;
            var uriBuilder = new UriBuilder
            {
                Scheme = useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                Host = nodesExt.Hostname,
                Port = useSsl ? nodesExt.Services.KvSsl : nodesExt.Services.Kv
            };

            var ipAddress = uriBuilder.Uri.GetIpAddress(false);//TODO support IPv6
            return new IPEndPoint(ipAddress, uriBuilder.Port);
        }
    }
}
