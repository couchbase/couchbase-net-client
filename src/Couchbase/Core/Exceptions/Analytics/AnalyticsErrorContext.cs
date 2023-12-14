using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using Couchbase.Core.Retry;
using Couchbase.Query;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Core.Exceptions.Analytics
{
    /// <remarks>Uncommitted</remarks>
    [InterfaceStability(Level.Uncommitted)]
    [DebuggerDisplay($"{{{nameof(DebuggerDisplay)}:nq}}")]
    public class AnalyticsErrorContext : IErrorContext
    {
        public string? Statement { get; internal set; }

        [JsonConverter(typeof(JsonStringEnumConverter<HttpStatusCode>))]
        public HttpStatusCode HttpStatus { get; internal set; }

        public string? ClientContextId { get; internal set; }

        public string? Message { get; internal set; }

        public string? Parameters { get; internal set; }

        public List<Error>? Errors { get; internal set; }

        public List<RetryReason>? RetryReasons { get; internal set; }

        private string DebuggerDisplay
        {
            // These requirements may be removed in the future if the Error object is always deserialized using System.Text.Json even when
            // DefaultSerializer is used for the rest of the response. Then this may be changed to use JsonSerializer directly instead of SerializeWithFallback.
            [RequiresUnreferencedCode("The Error object may contain Newtonsoft.Json.Linq.JToken objects in the AdditionalData property.")]
            [RequiresDynamicCode("The Error object may contain Newtonsoft.Json.Linq.JToken objects in the AdditionalData property.")]
            get => InternalSerializationContext.SerializeWithFallback(this, InternalSerializationContext.Default.AnalyticsErrorContext);
        }
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
