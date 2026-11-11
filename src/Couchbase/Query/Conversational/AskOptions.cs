using System;
using System.Threading;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational
{
    /// <summary>
    /// Optional arguments for <see cref="IChat.AskAsync{T}"/>.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public sealed class AskOptions
    {
        // Each request sends a copy.
        internal QueryOptions QueryOptions { get; } = new();

        internal string? HintValue { get; private set; }

        internal bool? ExecuteValue { get; private set; }

        internal ConversationalQueryOutput? OutputValue { get; private set; }

        /// <summary>
        /// Gives the model extra context for this prompt. An empty value leaves it unset.
        /// </summary>
        /// <param name="hint">The hint.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions Hint(string hint)
        {
            HintValue = hint;
            return this;
        }

        /// <summary>
        /// Sets whether Query runs an eligible generated statement. Query runs it by default.
        /// </summary>
        /// <param name="execute">False to only generate the statement.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions Execute(bool execute)
        {
            ExecuteValue = execute;
            return this;
        }

        /// <summary>
        /// Sets the kind of statement the model generates.
        /// </summary>
        /// <param name="output">The kind of statement.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions Output(ConversationalQueryOutput output)
        {
            OutputValue = output;
            return this;
        }

        /// <summary>
        /// Sets the index scan consistency of the generated statement.
        /// </summary>
        /// <param name="scanConsistency">The scan consistency.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions ScanConsistency(QueryScanConsistency scanConsistency)
        {
            QueryOptions.ScanConsistency(scanConsistency);
            return this;
        }

        /// <summary>
        /// Makes the generated statement see the given mutations.
        /// </summary>
        /// <param name="mutationState">The mutations to wait for.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions ConsistentWith(MutationState mutationState)
        {
            QueryOptions.ConsistentWith(mutationState);
            return this;
        }

        /// <summary>
        /// Sets the maximum time to wait for indexes to catch up.
        /// </summary>
        /// <param name="scanWait">The maximum wait.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions ScanWait(TimeSpan scanWait)
        {
            QueryOptions.ScanWait(scanWait);
            return this;
        }

        /// <summary>
        /// Allows the generated statement to read from replicas.
        /// </summary>
        /// <param name="useReplica">True to allow replica reads.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions UseReplica(bool useReplica)
        {
            QueryOptions.UseReplica(useReplica);
            return this;
        }

        /// <summary>
        /// Sets the client context ID. The SDK generates one by default.
        /// </summary>
        /// <param name="clientContextId">The client context ID.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions ClientContextId(string clientContextId)
        {
            QueryOptions.ClientContextId(clientContextId);
            return this;
        }

        /// <summary>
        /// Asks Query to return metrics. They are off by default.
        /// </summary>
        /// <param name="includeMetrics">True to return metrics.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions Metrics(bool includeMetrics)
        {
            QueryOptions.Metrics(includeMetrics);
            return this;
        }

        /// <summary>
        /// Asks Query to return a profile of the request.
        /// </summary>
        /// <param name="profile">The profile mode.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions Profile(QueryProfile profile)
        {
            QueryOptions.Profile(profile);
            return this;
        }

        /// <summary>
        /// Sets the timeout of the request. The cluster query timeout applies by default.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions Timeout(TimeSpan timeout)
        {
            QueryOptions.Timeout(timeout);
            return this;
        }

        /// <summary>
        /// Adds a Query request parameter that has no typed option.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The parameter value.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions Raw(string name, object value)
        {
            QueryOptions.Raw(name, value);
            return this;
        }

        /// <summary>
        /// Sets a token that cancels the request.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An <see cref="AskOptions"/> instance for chaining.</returns>
        public AskOptions CancellationToken(CancellationToken cancellationToken)
        {
            QueryOptions.CancellationToken(cancellationToken);
            return this;
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
