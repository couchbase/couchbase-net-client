using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Retry;

#nullable enable

namespace Couchbase.Core.Exceptions.View
{
    /// <remarks>Uncommitted</remarks>
    [InterfaceStability(Level.Uncommitted)]
    public class ViewContextError : IViewErrorContext
    {
        public string? DesignDocumentName { get; set; }

        public string? Parameters { get; set; }

        public string? ViewName { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HttpStatusCode HttpStatus { get; set; }

        public string? ClientContextId { get; set; }

        public string? Message { get; set; }

        public string? Errors { get; set; }

        public List<RetryReason>? RetryReasons { get; internal set; }

        public override string ToString() =>
            JsonSerializer.Serialize(this, InternalSerializationContext.Default.ViewContextError);
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
