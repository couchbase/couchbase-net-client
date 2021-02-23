using System.Collections.Generic;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Tracing
{
    [InterfaceStability(Level.Volatile)]
    internal static class InnerRequestSpans
    {
        /// <summary>
        /// The span for request encoding before dispatch
        /// </summary>
        internal static class EncodingSpan
        {
            public const string Name = "request_encoding";

            internal static class Attributes
            {
                /// <summary>
                ///     This attribute is a standard OpenTelemetry attribute and should be placed on all spans to uniquely identify them
                ///     for couchbase.
                /// </summary>
                public static KeyValuePair<string, string> System = new("db.system", "couchbase");
            }
        }

        /// <summary>
        /// The dispatch-span during which the operation is in-flight
        /// </summary>
        internal static class DispatchSpan
        {
            /// <summary>
            ///     The span name
            /// </summary>
            public const string Name = "dispatch_to_server";

            /// <summary>
            ///     Attributes of the dispatch-span.
            /// </summary>
            internal static class Attributes
            {
                /// <summary>
                ///     This attribute is a standard OpenTelemetry attribute and should be placed on all spans to uniquely identify them
                ///     for couchbase.
                /// </summary>
                public static KeyValuePair<string, string> System = new("db.system", "couchbase");

                /// <summary>
                ///     This attribute is a standard OpenTelemetry attribute and should be placed on every dispatch span.
                /// </summary>
                public static KeyValuePair<string, string> NetTransport = new("net.transport", "IP.TCP");

                /// <summary>
                ///     When the execution duration is reported by the server as part of the response, it should be included in
                ///     microseconds.
                /// </summary>
                public static string ServerDuration = "db.couchbase.server_duration";

                /// <summary>
                ///     The local ID is the connection ID used when creating the connection against the cluster. Note that right now the ID
                ///     is only populated for the KV service.
                /// </summary>
                public static string LocalId = "db.couchbase.local_id";

                /// <summary>
                ///     The hostname for the local side of the socket.
                /// </summary>
                public static string LocalHostname = "net.host.name";

                /// <summary>
                ///     The port for the local side of the socket.
                /// </summary>
                public static string LocalPort = "net.host.port";

                /// <summary>
                ///     The hostname for the remote side of the socket.
                /// </summary>
                public static string RemoteHostname = "net.peer.name";

                /// <summary>
                ///     The port for the remote side of the socket.
                /// </summary>
                public static string RemotePort = "net.peer.port";

                /// <summary>
                ///     The operation ID, together with the service type, allows to (likely) distinguish the request from others. The
                ///     operation ID is a string and depends on the service used.
                /// </summary>
                public static string OperationId = "db.couchbase.operation_id";
            }
        }
    }
}
