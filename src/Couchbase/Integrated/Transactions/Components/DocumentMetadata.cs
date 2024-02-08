#if NET5_0_OR_GREATER
#nullable enable
using Newtonsoft.Json;

namespace Couchbase.Integrated.Transactions.Components
{
    /// <summary>
    /// A POCO to serialize transactions metadata on a document for rollback / unstaging purposes.
    /// </summary>
    internal class DocumentMetadata
    {
        /// <summary>
        /// Gets the stringified CAS value.
        /// </summary>
        [JsonProperty("CAS")]
        public string? Cas { get; internal set; }

        /// <summary>
        /// Gets the Revision ID.
        /// </summary>
        [JsonProperty("revid")]
        public string? RevId { get; internal set; }

        /// <summary>
        /// Gets the expiration time
        /// </summary>
        [JsonProperty("exptime")]
        public ulong? ExpTime { get; internal set; }

        /// <summary>
        /// Gets the CRC32 checksum.
        /// </summary>
        [JsonProperty("value_crc32c")]
        public string? Crc32c { get; internal set; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
#endif
