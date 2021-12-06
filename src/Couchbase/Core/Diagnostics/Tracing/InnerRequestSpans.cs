using System.Collections.Generic;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Tracing
{
    [InterfaceStability(Level.Volatile)]
    internal static class InnerRequestSpans
    {
        /// <summary>
        /// The span for request compression before dispatch
        /// </summary>
        internal static class CompressionSpan
        {
            public const string Name = "request_compression";

            internal static class Attributes
            {
                /// <summary>
                ///     This attribute is a standard OpenTelemetry attribute and should be placed on all spans to uniquely identify them
                ///     for couchbase.
                /// </summary>
                public static readonly KeyValuePair<string, string> System = new("db.system", "couchbase");

                public const string CompressionRatio = "compression.ratio";

                public const string CompressionUsed = "compression.used";
            }
        }

        /// <summary>
        /// The span for response decompression
        /// </summary>
        internal static class DecompressionSpan
        {
            public const string Name = "response_decompression";

            internal static class Attributes
            {
                /// <summary>
                ///     This attribute is a standard OpenTelemetry attribute and should be placed on all spans to uniquely identify them
                ///     for couchbase.
                /// </summary>
                public static readonly KeyValuePair<string, string> System = new("db.system", "couchbase");
            }
        }

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
                public static readonly KeyValuePair<string, string> System = new("db.system", "couchbase");
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
                public static readonly KeyValuePair<string, string> System = new("db.system", "couchbase");

                /// <summary>
                ///     This attribute is a standard OpenTelemetry attribute and should be placed on every dispatch span.
                /// </summary>
                public static readonly KeyValuePair<string, string> NetTransport = new("net.transport", "IP.TCP");

                /// <summary>
                ///     When the execution duration is reported by the server as part of the response, it should be included in
                ///     microseconds.
                /// </summary>
                public const string ServerDuration = "db.couchbase.server_duration";

                /// <summary>
                ///     The local ID is the connection ID used when creating the connection against the cluster. Note that right now the ID
                ///     is only populated for the KV service.
                /// </summary>
                public const string LocalId = "db.couchbase.local_id";

                /// <summary>
                ///     The hostname for the local side of the socket.
                /// </summary>
                public const string LocalHostname = "net.host.name";

                /// <summary>
                ///     The port for the local side of the socket.
                /// </summary>
                public const string LocalPort = "net.host.port";

                /// <summary>
                ///     The hostname for the remote side of the socket.
                /// </summary>
                public const string RemoteHostname = "net.peer.name";

                /// <summary>
                ///     The port for the remote side of the socket.
                /// </summary>
                public const string RemotePort = "net.peer.port";

                /// <summary>
                ///     The operation ID, together with the service type, allows to (likely) distinguish the request from others. The
                ///     operation ID is a string and depends on the service used.
                /// </summary>
                public const string OperationId = "db.couchbase.operation_id";

                /// <summary>
                /// The operation timeout in milliseconds
                /// </summary>
                public const string TimeoutMilliseconds = "timeout_ms";
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
