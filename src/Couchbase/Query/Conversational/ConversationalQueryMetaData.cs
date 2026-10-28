using System;
using System.Collections.Generic;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational
{
    /// <summary>
    /// The metadata of a conversational query or chat response.
    /// </summary>
    /// <remarks>
    /// Query returns metrics, and so <see cref="NaturalLanguageProcessingTime"/> and <see cref="GeneratedStatementExecuted"/>,
    /// only when metrics are turned on in the options.
    /// </remarks>
    [InterfaceStability(Level.Volatile)]
    public sealed class ConversationalQueryMetaData
    {
        // Reads through, so streamed metrics show up once the rows are read.
        private readonly QueryMetaData? _metaData;
        private readonly NaturalLanguageResponse? _naturalLanguage;

        internal ConversationalQueryMetaData(QueryMetaData? metaData, NaturalLanguageResponse? naturalLanguage)
        {
            _metaData = metaData;
            _naturalLanguage = naturalLanguage;
        }

        /// <summary>
        /// The unique ID of the response.
        /// </summary>
        public string RequestId => _metaData?.RequestId ?? string.Empty;

        /// <summary>
        /// The client context ID of the request.
        /// </summary>
        public string ClientContextId => _metaData?.ClientContextId ?? string.Empty;

        /// <summary>
        /// The status of the request.
        /// </summary>
        public QueryStatus Status => _metaData?.Status ?? default;

        /// <summary>
        /// The warnings Query returned.
        /// </summary>
        public IReadOnlyList<QueryWarning> Warnings =>
            (IReadOnlyList<QueryWarning>?)_metaData?.Warnings ?? Array.Empty<QueryWarning>();

        /// <summary>
        /// The metrics of the request, if Query returned them.
        /// </summary>
        public QueryMetrics? Metrics => _metaData?.Metrics;

        /// <summary>
        /// The profile of the request, if one was requested.
        /// </summary>
        public dynamic? Profile => _metaData?.Profile;

        /// <summary>
        /// The model tokens this request used.
        /// </summary>
        public TokenUsage? RequestTokens => _naturalLanguage?.RequestTokens;

        /// <summary>
        /// The model tokens the chat used so far.
        /// </summary>
        public TokenUsage? ChatTokens => _naturalLanguage?.ChatTokens;

        /// <summary>
        /// The time Query spent on the model. Returned only with metrics.
        /// </summary>
        public TimeSpan? NaturalLanguageProcessingTime => _naturalLanguage?.NaturalLanguageProcessingTime;

        /// <summary>
        /// True if Query ran the generated statement. Returned only with metrics.
        /// </summary>
        public bool? GeneratedStatementExecuted => _naturalLanguage?.GeneratedStatementExecuted;
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
