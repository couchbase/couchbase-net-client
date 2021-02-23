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
