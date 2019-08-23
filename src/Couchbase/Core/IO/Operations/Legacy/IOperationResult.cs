namespace Couchbase.Core.IO.Operations.Legacy
{
    /// <summary>
    /// The primary return type for binary Memcached operations
    /// </summary>
    public interface IOperationResult : IResult
    {
        /// <summary>
        /// Gets the mutation token for the operation if enhanced durability is enabled.
        /// </summary>
        /// <value>
        /// The mutation token.
        /// </value>
        /// <remarks>Note: this is used internally for enhanced durability if supported by
        /// the Couchbase server version and enabled by clusterOptions.</remarks>
        MutationToken Token { get; }

        /// <summary>
        /// The 'Check and Set' or 'CAS' value for enforcing optimistic concurrency.
        /// </summary>
        ulong Cas { get; set; }

        /// <summary>
        /// The server's response status for the operation.
        /// </summary>
        ResponseStatus Status { get; }

        /// <summary>
        /// The level of durability that the operation achieved
        /// </summary>
        Durability Durability { get; set; }

        /// <summary>
        /// Checks if the server responded with a Not My Vbucket.
        /// </summary>
        /// <returns>Returns true if <see cref="ResponseStatus"/> is a VBucketBelongsToAnotherServer.</returns>
        bool IsNmv();

        /// <summary>
        /// Gets the id or key for the document.
        /// </summary>
        /// <value>
        /// The identifier.
        /// </value>
        string Id { get; }

        /// <summary>
        /// Gets the op code.
        /// </summary>
        /// <value>
        /// The op code.
        /// </value>
        OpCode OpCode { get; }
    }
}
#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion
