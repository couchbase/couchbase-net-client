#nullable enable
using System;

namespace Couchbase.Core.Diagnostics
{
    /// <summary>
    /// Controls whether Couchbase spans emit legacy attribute names (default)
    /// or modern OpenTelemetry semantic-convention names.
    /// This is a process-global setting: if any cluster enables modern mode,
    /// the entire process emits modern attribute names.
    /// </summary>
    public enum ObservabilitySemanticConvention
    {
        /// <summary>
        /// Legacy attribute names are the default, which couchbase has historically used.
        /// </summary>
        Legacy = 0,
        /// <summary>
        /// Modern attribute names are OpenTelemetry semantic-convention names.
        /// </summary>
        Modern = 1,
        /// <summary>
        /// Both legacy and modern attribute names are emitted.  Helpful while transitioning
        /// from one to the other.
        /// </summary>
        Both = 2
    }


    /// <summary>
    /// Parser for the OTEL_SEMCONV_STABILITY_OPT_IN environment variable.
    /// </summary>
    public static class ObservabilitySemanticConventionParser
    {
        private static readonly Lazy<ObservabilitySemanticConvention> CachedFromEnvironment =
            new(() => Parse(Environment.GetEnvironmentVariable("OTEL_SEMCONV_STABILITY_OPT_IN")));

        /// <summary>
        /// Get the current observability semantic convention mode from the environment variable.
        /// The result is cached after the first call.
        /// </summary>
        /// <returns>ObservabilitySemanticConvention from the environment, or Legacy by default.
        /// </returns>
        public static ObservabilitySemanticConvention FromEnvironment() => CachedFromEnvironment.Value;

        internal static ObservabilitySemanticConvention Parse(string? raw)
        {
            var mode = ObservabilitySemanticConvention.Legacy;
            if (raw is null) return mode;

#if NET6_0_OR_GREATER
            // TrimEntries avoids per-element Trim() calls and substring allocations.
            foreach (var token in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
#else
            foreach (var part in raw.Split(','))
            {
                if (part.Length == 0) continue;
                var token = part.Trim();
#endif
                if (token.Equals("database/dup", StringComparison.OrdinalIgnoreCase))
                    return ObservabilitySemanticConvention.Both;

                if (token.Equals("database", StringComparison.OrdinalIgnoreCase))
                    mode = ObservabilitySemanticConvention.Modern;
            }

            return mode;
        }
    }
}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
