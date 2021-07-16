using System.Collections.Generic;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public static class ServiceIdentifier
    {

        public static readonly ISet<string> CoreServices = new HashSet<string>
        {
            Data,
            Query,
            Search,
            Views,
            Analytics
        };

        /// <summary>
        /// The data or "K/V" service.
        /// </summary>
        public const string Data = "kv";

        /// <summary>
        /// The query or "N1QL" service.
        /// </summary>
        public const string Query = "query";

        /// <summary>
        /// The search or "FTS" service.
        /// </summary>
        public const string Search = "search";

        /// <summary>
        /// The views service.
        /// </summary>
        public const string Views = "views";

        /// <summary>
        /// The analytics service.
        /// </summary>
        public const string Analytics = "analytics";

        /// <summary>
        /// The management service (“ns_server” / 8091)
        /// </summary>
        public const string Management = "management";
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
