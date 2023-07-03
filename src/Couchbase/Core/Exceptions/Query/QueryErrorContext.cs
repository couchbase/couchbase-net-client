using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers.SystemTextJson;
using Couchbase.Core.Retry;
using Couchbase.Query;

#nullable enable

namespace Couchbase.Core.Exceptions.Query
{
    /// <remarks>Uncommitted</remarks>
    [InterfaceStability(Level.Uncommitted)]
    public class QueryErrorContext : IQueryErrorContext, IKeyValueErrorContext
    {
        [JsonInclude]
        public string? Statement { get; set; }

        [JsonInclude]
        public string? ClientContextId { get; set; }

        [JsonInclude]
        public string? Parameters { get; set; }

        [JsonInclude]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HttpStatusCode HttpStatus { get; set; }

        [JsonInclude]
        [JsonConverter(typeof(CamelCaseStringEnumConverter))]
        public QueryStatus QueryStatus { get; set; }

        [JsonInclude]
        public List<Error>? Errors { get; set; }

        [JsonInclude]
        public string? Message { get; set; }

        public override string ToString() =>
            InternalSerializationContext.SerializeWithFallback(this, QuerySerializerContext.Default.QueryErrorContext);

        public List<RetryReason>? RetryReasons { get; internal set; }

        #region IKeyValueErrorContext

        /*
         * Note that in order for KV exceptions like DocumentNotFoundException and DocumentExistsException
         * we need to coerce the treatment of the error context as they are very different from the KV
         * context which is Bucket/Scope/Collection level to Query context which is cluster level. Now
         * that Query throws KV exceptions this allows to do this in a non backward breaking manner.
         */

        [JsonIgnore]
        string? IKeyValueErrorContext.DispatchedFrom => default;

        [JsonIgnore]
        string? IKeyValueErrorContext.DispatchedTo => default;

        [JsonIgnore]
        string? IKeyValueErrorContext.DocumentKey => default;

        [JsonIgnore]
        string? IKeyValueErrorContext.ClientContextId => default;

        [JsonIgnore]
        ulong IKeyValueErrorContext.Cas => default;

        [JsonIgnore]
        ResponseStatus IKeyValueErrorContext.Status => default;

        [JsonIgnore]
        string? IKeyValueErrorContext.BucketName => default;

        [JsonIgnore]
        string? IKeyValueErrorContext.CollectionName => default;

        [JsonIgnore]
        string? IKeyValueErrorContext.ScopeName => default;

        [JsonIgnore]
        OpCode IKeyValueErrorContext.OpCode { get; set; } = default;

        #endregion
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
