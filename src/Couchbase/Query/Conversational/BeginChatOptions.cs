using System;
using System.Threading;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Diagnostics.Tracing;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// Optional arguments for <see cref="IConversationalQuery.BeginChatAsync"/>.
/// </summary>
[InterfaceStability(Level.Volatile)]
public sealed class BeginChatOptions
{
    internal QueryOptions QueryOptions { get; } = new();

    internal ModelOptions? ModelValue { get; private set; }

    internal ModelCredential? CredentialValue { get; private set; }

    internal TimeSpan? InactivityTimeoutValue { get; private set; }

    internal bool UseKnowledgeValue { get; private set; }

    /// <summary>
    /// Sets the model Query calls. The chat keeps it and sends it with each prompt.
    /// </summary>
    /// <param name="model">The model options.</param>
    /// <returns>A <see cref="BeginChatOptions"/> instance for chaining.</returns>
    public BeginChatOptions Model(ModelOptions model)
    {
        ModelValue = model;
        return this;
    }

    /// <summary>
    /// Sets the credential Query uses to call the model. The chat keeps it and sends it with each prompt.
    /// </summary>
    /// <param name="credential">The model credential.</param>
    /// <returns>A <see cref="BeginChatOptions"/> instance for chaining.</returns>
    public BeginChatOptions Credential(ModelCredential credential)
    {
        CredentialValue = credential;
        return this;
    }

    /// <summary>
    /// Sets how long the chat can stay idle before Query ends it. It is sent in whole seconds, rounded up.
    /// </summary>
    /// <param name="inactivityTimeout">The idle time. Zero leaves the Query default.</param>
    /// <returns>A <see cref="BeginChatOptions"/> instance for chaining.</returns>
    public BeginChatOptions InactivityTimeout(TimeSpan inactivityTimeout)
    {
        InactivityTimeoutValue = inactivityTimeout;
        return this;
    }

    /// <summary>
    /// Sets whether the chat uses the Knowledge entries of Query. It is off by default.
    /// </summary>
    /// <param name="useKnowledge">True to use Knowledge.</param>
    /// <returns>A <see cref="BeginChatOptions"/> instance for chaining.</returns>
    public BeginChatOptions UseKnowledge(bool useKnowledge)
    {
        UseKnowledgeValue = useKnowledge;
        return this;
    }

    /// <summary>
    /// Sets the timeout of the request. The cluster query timeout applies by default.
    /// </summary>
    /// <param name="timeout">The timeout.</param>
    /// <returns>A <see cref="BeginChatOptions"/> instance for chaining.</returns>
    public BeginChatOptions Timeout(TimeSpan timeout)
    {
        QueryOptions.Timeout(timeout);
        return this;
    }

    /// <summary>
    /// A parent or external span for tracing.
    /// </summary>
    /// <param name="span">An external <see cref="IRequestSpan"/> implementation for tracing.</param>
    /// <returns>A <see cref="BeginChatOptions"/> instance for chaining.</returns>
    public BeginChatOptions RequestSpan(IRequestSpan span)
    {
        QueryOptions.RequestSpan(span);
        return this;
    }

    /// <summary>
    /// Sets a token that cancels the request.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="BeginChatOptions"/> instance for chaining.</returns>
    public BeginChatOptions CancellationToken(CancellationToken cancellationToken)
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
