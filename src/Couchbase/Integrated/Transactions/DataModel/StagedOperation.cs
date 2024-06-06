#nullable enable
using Newtonsoft.Json;

#pragma warning disable CS1591

namespace Couchbase.Integrated.Transactions.DataModel
{
    // TODO: this class should be made internal
    internal class StagedOperation
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        // TODO: while this is part of the data model, the fact that we have to read separately so that we
        // have access to LookupInResult.ContentAs<T>() later means that we're keeping this in memory twice.
        // That could be significant if the document is large.
        [JsonProperty("stgd")]
        public object? StagedDocument { get; set; }

        [JsonProperty("crc32")]
        public string? Crc32 { get; set; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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





