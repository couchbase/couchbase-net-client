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
             "The SDK will continue to try to connect every {1}ms. " +
             "Until it can connect every operation routed to it will fail with this exception.";

        public static string BootStrapFailedMsg =
            "After bootstrapping the nodes list has zero elements indicating that " +
            "client failed during bootstrapping. Please check the client logs for more " +
            "information as for why it failed.";

        public static string DocumentMutationLostMsg = "Document Mutation lost during a hard failover.";

        public static string NotEnoughReplicasConfigured = "Not enough replicas configured on the bucket.";

        public static string RemoteHostClosedMsg = "The remote host ({0}) has gracefully closed this connection.";

        public static string GetNodeUnavailableMsg(IPEndPoint ipEndPoint, uint interval)
        {
            return string.Format(NodeUnavailableMsg, ipEndPoint, interval);
        }

        public static string GetMessage(string msg, params object[] args)
        {
            return string.Format(msg, args);
        }
    }
}
