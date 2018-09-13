namespace Couchbase.Tracing
{
    internal static class CouchbaseTags
    {
        public const string DbTypeCouchbase = "couchbase";

        public const string Service = "couchbase.service";
        public const string ServiceKv = "kv";
        public const string ServiceView = "view";
        public const string ServiceQuery = "n1ql";
        public const string ServiceSearch = "fts";
        public const string ServiceAnalytics = "cbas";

        public const string OperationId = "couchbase.operation_id";
        public const string DocumentKey = "couchbase.document_key";
        public const string LocalId = "couchbase.local_id";
        public const string Ignore = "couchbase.ignore";

        public const string LocalAddress = "local.address";
        public const string PeerLatency = "peer.latency";
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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

#endregion
