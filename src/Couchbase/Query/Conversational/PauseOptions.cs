using System;
using System.Threading;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// Optional arguments for <see cref="IChat.PauseAsync"/>.
/// </summary>
[InterfaceStability(Level.Volatile)]
public sealed class PauseOptions
{
    // Each request sends a copy.
    internal QueryOptions QueryOptions { get; } = new();

    internal bool? SummarizeValue { get; private set; }

    /// <summary>
    /// Sets whether Query may summarize the chat with the model before it saves it. False does not send the model.
    /// </summary>
    /// <param name="summarize">False to save the chat without calling the model.</param>
    /// <returns>A <see cref="PauseOptions"/> instance for chaining.</returns>
    public PauseOptions Summarize(bool summarize)
    {
        SummarizeValue = summarize;
        return this;
    }

    /// <summary>
    /// Sets the timeout of the request. The cluster query timeout applies by default.
    /// </summary>
    /// <param name="timeout">The timeout.</param>
    /// <returns>A <see cref="PauseOptions"/> instance for chaining.</returns>
    public PauseOptions Timeout(TimeSpan timeout)
    {
        QueryOptions.Timeout(timeout);
        return this;
    }

    /// <summary>
    /// Sets a token that cancels the request.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="PauseOptions"/> instance for chaining.</returns>
    public PauseOptions CancellationToken(CancellationToken cancellationToken)
    {
        QueryOptions.CancellationToken(cancellationToken);
        return this;
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
