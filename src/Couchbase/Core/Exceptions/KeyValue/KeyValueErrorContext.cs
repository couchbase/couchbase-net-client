using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers.SystemTextJson;

#nullable enable

namespace Couchbase.Core.Exceptions.KeyValue
{
    /// <remarks>Uncommitted</remarks>
    [InterfaceStability(Level.Uncommitted)]
    public class KeyValueErrorContext : IKeyValueErrorContext
    {
        public string? DispatchedFrom { get; set; } //state.localendpoint

        public string? DispatchedTo { get; set; } //state.endpoint

        public string? DocumentKey { get; set; } //op.Id

        public string? ClientContextId { get; set; } //state.opaque||op.opaque

        public ulong Cas { get; set; } //op.Cas

        [JsonConverter(typeof(CamelCaseStringEnumConverter))]
        public ResponseStatus Status { get; set; } //state.Status

        public string? BucketName { get; set; } //collection.Bucket.BucketName

        public string? CollectionName { get; set; }//collecton.Name

        public string? ScopeName { get; set; }//scope.name

        public string? Message { get; set; } //errorcode

        [JsonConverter(typeof(CamelCaseStringEnumConverter))]
        public OpCode OpCode { get; set; }

        public override string ToString() =>
            JsonSerializer.Serialize(this, InternalSerializationContext.Default.KeyValueErrorContext);
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
