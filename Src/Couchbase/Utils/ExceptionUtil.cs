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
        public static string ServiceNotSupportedMsg =
            "A request has been made for a service that is not configured or supported by the cluster. " +
            "Please check the cluster and enable or add a new node with the requested service: {0}.";

        public static string NodeUnavailableMsg =
            "The node {0} that the key was mapped to is either down or unreachable. " +
             "The SDK will continue to try to connect every {1}ms. " +
             "Until it can connect every operation routed to it will fail with this exception.";

        public static string BootStrapFailedMsg =
            "After bootstrapping the nodes list has zero elements indicating that " +
            "client failed during bootstrapping. Please check the client logs for more " +
            "information as for why it failed.";

        public static string RemoteHostTimeoutMsg =
            "The connection has timed out while an operation was in flight. The current setting for SendTimeout is {0}ms.";

        public static string DocumentMutationLostMsg = "Document Mutation lost during a hard failover.";

        public static string NotEnoughReplicasConfigured = "Not enough replicas configured on the bucket.";

        public static string RemoteHostClosedMsg = "The remote host ({0}) has gracefully closed this connection.";

        public static string TemporaryLockErrorMsg = "A temporary lock error was detected for key: {0}";

        public static string DocumentExistsMsg = "An existing document was found for key: {0}";

        public static string DocumentNotFoundMsg = "No document found for key: {0}";

        public static string CasMismatchMsg = "The CAS value has changed for key: {0}";

        public static string InvalidOpCodeMsg = "Unknown status for opcode {0} and key {1}: {2}";

        public const string FailedBucketAuthenticationMsg = "Authentication failed for bucket '{0}'";

        public const string EmptyKeyErrorMsg = "Key cannot be null or empty.";

        public const string BucketCredentialsMissingMsg = "Bucket credentials missing for `{0}`.";

        public const string PoolConfigNumberOfConnections = "The {0} number of connetions to create must be between {1} and {2}.";

        public const string PoolConfigMaxGreaterThanMin = "The maximum number of connections ({0}) to create cannot be lower than the minimum ({1})";

        public const string MissingOrEmptyServerResolverType = "Missing or empty server resolver type.";

        public const string ErrorRetrievingServersUsingServerResolver = "Error retrieving servers using resolver '{0}'.";

        public const string UnrecognisedServerResolverType = "Unable to find or build type '{0}' as a server resolver.";

        public const string ServerResolverTypeDoesntImplementInterface = "Unable to use type '{0}' as a server resolver because it does not conform to interface '{1}'.";

        public const string ServerResolverReturnedNoservers = "Did not find any servers using custom resolver '{0}'.";

        public static string GetNodeUnavailableMsg(IPEndPoint ipEndPoint, uint interval)
        {
            return string.Format(NodeUnavailableMsg, ipEndPoint, interval);
        }

        public static string GetMessage(string msg, params object[] args)
        {
            return string.Format(msg, args);
        }

        public static string WithParams(this string msg, params object[] args)
        {
            return GetMessage(msg, args);
        }
    }
}
