using Couchbase.Core.Buckets;

namespace Couchbase
{
    /// <summary>
    /// Provides an interface for interacting with documents within Couchbase Server
    /// </summary>
    /// <typeparam name="T">The type of document.</typeparam>
    public interface IDocument<T>
    {
        /// <summary>
        /// The unique identifier for the document
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// The "Check and Set" value for enforcing optimistic concurrency
        /// </summary>
        ulong Cas { get; set; }

        /// <summary>
        /// The time-to-live or TTL for the document before it's evicted from disk in milliseconds.
        /// </summary>
        /// <remarks>Setting this to zero or less will give the document infinite lifetime</remarks>
        uint Expiry { get; set; }

        /// <summary>
        /// The value representing the document itself
        /// </summary>
        T Content { get; set; }

        /// <summary>
        /// Gets the mutation token for the operation if enhanced durability is enabled.
        /// </summary>
        /// <value>
        /// The mutation token.
        /// </value>
        /// <remarks>Note: this is used internally for enhanced durability if supported by
        /// the Couchbase server version and enabled by configuration.</remarks>
        MutationToken Token { get; }
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
