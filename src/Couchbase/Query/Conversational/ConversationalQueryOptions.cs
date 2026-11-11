using System;
using System.Threading;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// Optional arguments for <see cref="IConversationalQuery.QueryAsync{T}"/>.
/// </summary>
[InterfaceStability(Level.Volatile)]
public sealed class ConversationalQueryOptions
{
    // Each request sends a copy.
    internal QueryOptions QueryOptions { get; } = new();

    internal ModelOptions? ModelValue { get; private set; }

    internal ModelCredential? CredentialValue { get; private set; }

    internal string? HintValue { get; private set; }

    internal bool? ExecuteValue { get; private set; }

    internal ConversationalQueryOutput? OutputValue { get; private set; }

    internal bool? UseKnowledgeValue { get; private set; }

    /// <summary>
    /// Sets the model Query calls.
    /// </summary>
    /// <param name="model">The model options.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Model(ModelOptions model)
    {
        ModelValue = model;
        return this;
    }

    /// <summary>
    /// Sets the credential Query uses to call the model.
    /// </summary>
    /// <param name="credential">The model credential.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Credential(ModelCredential credential)
    {
        CredentialValue = credential;
        return this;
    }

    /// <summary>
    /// Gives the model extra context for this prompt. An empty value leaves it unset.
    /// </summary>
    /// <param name="hint">The hint.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Hint(string hint)
    {
        HintValue = hint;
        return this;
    }

    /// <summary>
    /// Sets whether Query runs an eligible generated statement. Query runs it by default.
    /// </summary>
    /// <param name="execute">False to only generate the statement.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Execute(bool execute)
    {
        ExecuteValue = execute;
        return this;
    }

    /// <summary>
    /// Sets the kind of statement the model generates.
    /// </summary>
    /// <param name="output">The kind of statement.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Output(ConversationalQueryOutput output)
    {
        OutputValue = output;
        return this;
    }

    /// <summary>
    /// Sets whether Query uses its Knowledge entries for this prompt.
    /// </summary>
    /// <param name="useKnowledge">True to use Knowledge.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions UseKnowledge(bool useKnowledge)
    {
        UseKnowledgeValue = useKnowledge;
        return this;
    }

    /// <summary>
    /// Sets the index scan consistency of the generated statement.
    /// </summary>
    /// <param name="scanConsistency">The scan consistency.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions ScanConsistency(QueryScanConsistency scanConsistency)
    {
        QueryOptions.ScanConsistency(scanConsistency);
        return this;
    }

    /// <summary>
    /// Makes the generated statement see the given mutations.
    /// </summary>
    /// <param name="mutationState">The mutations to wait for.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions ConsistentWith(MutationState mutationState)
    {
        QueryOptions.ConsistentWith(mutationState);
        return this;
    }

    /// <summary>
    /// Sets the maximum time to wait for indexes to catch up.
    /// </summary>
    /// <param name="scanWait">The maximum wait.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions ScanWait(TimeSpan scanWait)
    {
        QueryOptions.ScanWait(scanWait);
        return this;
    }

    /// <summary>
    /// Allows the generated statement to read from replicas.
    /// </summary>
    /// <param name="useReplica">True to allow replica reads.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions UseReplica(bool useReplica)
    {
        QueryOptions.UseReplica(useReplica);
        return this;
    }

    /// <summary>
    /// Sets the client context ID. The SDK generates one by default.
    /// </summary>
    /// <param name="clientContextId">The client context ID.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions ClientContextId(string clientContextId)
    {
        QueryOptions.ClientContextId(clientContextId);
        return this;
    }

    /// <summary>
    /// Asks Query to return metrics. They are off by default.
    /// </summary>
    /// <param name="includeMetrics">True to return metrics.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Metrics(bool includeMetrics)
    {
        QueryOptions.Metrics(includeMetrics);
        return this;
    }

    /// <summary>
    /// Asks Query to return a profile of the request.
    /// </summary>
    /// <param name="profile">The profile mode.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Profile(QueryProfile profile)
    {
        QueryOptions.Profile(profile);
        return this;
    }

    /// <summary>
    /// Sets the timeout of the request. The cluster query timeout applies by default.
    /// </summary>
    /// <param name="timeout">The timeout.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Timeout(TimeSpan timeout)
    {
        QueryOptions.Timeout(timeout);
        return this;
    }

    /// <summary>
    /// Adds a Query request parameter that has no typed option.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions Raw(string name, object value)
    {
        QueryOptions.Raw(name, value);
        return this;
    }

    /// <summary>
    /// Sets a token that cancels the request.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="ConversationalQueryOptions"/> instance for chaining.</returns>
    public ConversationalQueryOptions CancellationToken(CancellationToken cancellationToken)
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
