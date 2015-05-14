using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;

namespace Couchbase.Utils
{
    public static class ExceptionUtil
    {
        public static string NodeUnavailableMsg =
            "The node {0} that the key was mapped to is either down or unreachable. " +
             "The SDK will continue to try to connect every {1} seconds. " +
             "Until it can connect every operation routed to it will fail with this exception.";

        public static string GetNodeUnavailableMsg(IPEndPoint ipEndPoint, uint interval)
        {
            return string.Format(NodeUnavailableMsg, ipEndPoint, interval);
        }
    }
}
