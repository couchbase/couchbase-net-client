using System;
using System.Collections.Generic;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// The metadata of a conversational query or chat response.
/// </summary>
[InterfaceStability(Level.Volatile)]
public sealed class ConversationalQueryMetaData
{
    // Reads through, so streamed metrics show up once the rows are read.
    private readonly QueryMetaData _metaData;
    private readonly NaturalLanguageResponse _naturalLanguage;

    /// <summary>
    /// Creates empty metadata, for example to return from a fake <see cref="IChat"/> in tests.
    /// </summary>
    public ConversationalQueryMetaData()
        : this(null, null)
    {
    }

    internal ConversationalQueryMetaData(QueryMetaData? metaData, NaturalLanguageResponse? naturalLanguage)
    {
        _metaData = metaData ?? new QueryMetaData { Metrics = null };
        _naturalLanguage = naturalLanguage ?? new NaturalLanguageResponse();
    }

    /// <summary>
    /// The ID that Query assigned to the request.
    /// </summary>
    public string RequestId
    {
        get => _metaData.RequestId ?? string.Empty;
        init => _metaData.RequestId = value;
    }

    /// <summary>
    /// The client context ID of the request.
    /// </summary>
    public string ClientContextId
    {
        get => _metaData.ClientContextId ?? string.Empty;
        init => _metaData.ClientContextId = value;
    }

    /// <summary>
    /// The status of the request.
    /// </summary>
    public QueryStatus Status
    {
        get => _metaData.Status;
        init => _metaData.Status = value;
    }

    /// <summary>
    /// The warnings Query returned.
    /// </summary>
    public IReadOnlyList<QueryWarning> Warnings
    {
        get => (IReadOnlyList<QueryWarning>?)_metaData.Warnings ?? Array.Empty<QueryWarning>();
        init => _metaData.Warnings = new List<QueryWarning>(value);
    }

    /// <summary>
    /// The metrics of the request, if Query returned them.
    /// </summary>
    public QueryMetrics? Metrics
    {
        get => _metaData.Metrics;
        init => _metaData.Metrics = value;
    }

    /// <summary>
    /// The profile of the request, if one was requested.
    /// </summary>
    public dynamic? Profile
    {
        get => _metaData.Profile;
        init => _metaData.Profile = value;
    }

    /// <summary>
    /// The model tokens this request used.
    /// </summary>
    public TokenUsage? RequestTokens
    {
        get => _naturalLanguage.RequestTokens;
        init => _naturalLanguage.RequestTokens = value;
    }

    /// <summary>
    /// The model tokens the chat used so far.
    /// </summary>
    public TokenUsage? ChatTokens
    {
        get => _naturalLanguage.ChatTokens;
        init => _naturalLanguage.ChatTokens = value;
    }

    /// <summary>
    /// The time Query spent on the model. Returned only with metrics.
    /// </summary>
    public TimeSpan? NaturalLanguageProcessingTime
    {
        get => _naturalLanguage.NaturalLanguageProcessingTime;
        init => _naturalLanguage.NaturalLanguageProcessingTime = value;
    }

    /// <summary>
    /// True if Query ran the generated statement. Returned only with metrics.
    /// </summary>
    public bool? GeneratedStatementExecuted
    {
        get => _naturalLanguage.GeneratedStatementExecuted;
        init => _naturalLanguage.GeneratedStatementExecuted = value;
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
