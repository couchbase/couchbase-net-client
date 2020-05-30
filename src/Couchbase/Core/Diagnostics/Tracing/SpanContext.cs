using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal class SpanContext : ISpanContext
    {
        public string TraceId { get; }
        public string SpanId { get; }

        private IEnumerable<KeyValuePair<string, string>> Baggage { get; }

        public SpanContext(string traceId, string spanId, IEnumerable<KeyValuePair<string, string>> baggage)
        {
            TraceId = traceId;
            SpanId = spanId;
            Baggage = baggage;
        }

        public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
        {
            return Baggage ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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
