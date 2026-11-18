using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// The number of model tokens a request or a chat used, as reported by Query.
/// </summary>
[InterfaceStability(Level.Volatile)]
public sealed class TokenUsage
{
    /// <summary>
    /// The number of tokens in the prompts sent to the model.
    /// </summary>
    public int? PromptTokens { get; init; }

    /// <summary>
    /// The number of tokens the model generated.
    /// </summary>
    public int? CompletionTokens { get; init; }

    /// <summary>
    /// The total number of tokens.
    /// </summary>
    public int? TotalTokens { get; init; }
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
