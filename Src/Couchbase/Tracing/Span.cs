using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTracing;

namespace Couchbase.Tracing
{
    internal class Span : ISpan
    {
        private readonly long _startTimestamp;
        private long? _endTimestamp;

        internal ITracer Tracer { get; }
        internal List<Reference> References { get; }

        public string OperationName { get; private set; }
        public ISpanContext Context { get; }
        public Dictionary<string, object> Tags { get; }
        public Dictionary<string, string> Baggage { get; } = new Dictionary<string, string>();
        public List<Span> Spans { get; } = new List<Span>();

        internal Span(ITracer tracer, string operationName, ISpanContext context, long startTimestamp, Dictionary<string, object> tags, List<Reference> references)
        {
            Tracer = tracer;
            OperationName = operationName;
            Context = context;
            _startTimestamp = startTimestamp;
            Tags = tags ?? new Dictionary<string, object>();
            References = references ?? new List<Reference>();
        }

        public bool IsRootSpan => !References.Any();

        public bool ContainsIgnore => Tags.TryGetValue(CouchbaseTags.Ignore, out var value) && (bool) value;

        public bool ContainsService => Tags.ContainsKey(CouchbaseTags.Service);

        public long Duration
        {
            get
            {
                if (_endTimestamp.HasValue)
                {
                    return (_endTimestamp.Value - _startTimestamp) / 10;
                }
                return 0;
            }
        }

        public ISpan SetOperationName(string operationName)
        {
            OperationName = operationName;
            return this;
        }

        public ISpan SetTag(string key, bool value)
        {
            Tags.Add(key, value);
            return this;
        }

        public ISpan SetTag(string key, double value)
        {
            Tags.Add(key, value);
            return this;
        }

        public ISpan SetTag(string key, int value)
        {
            Tags.Add(key, value);
            return this;
        }

        public ISpan SetTag(string key, string value)
        {
            Tags.Add(key, value);
            return this;
        }

        public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
        {
            return Log(DateTimeOffset.UtcNow, fields);
        }

        public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            // noop
            return this;
        }

        public ISpan Log(string eventName)
        {
            return Log(DateTimeOffset.UtcNow, eventName);
        }

        public ISpan Log(DateTimeOffset timestamp, string eventName)
        {
            // noop
            return this;
        }

        public ISpan SetBaggageItem(string key, string value)
        {
            Baggage.Add(key, value);
            return this;
        }

        public string GetBaggageItem(string key)
        {
            return Baggage.TryGetValue(key, out var value) ? value : null;
        }

        public void Finish()
        {
            if (_endTimestamp.HasValue)
            {
                // span has already been stopped ..
                return;
            }

            _endTimestamp = Stopwatch.GetTimestamp();

            if (Tracer is ThresholdLoggingTracer tracer)
            {
                tracer.ReportSpan(this);
            }
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            if (_endTimestamp.HasValue)
            {
                // span has already been stopped ..
                return;
            }

            _endTimestamp = finishTimestamp.Ticks;

            if (Tracer is ThresholdLoggingTracer tracer)
            {
                tracer.ReportSpan(this);
            }
        }

        public void Dispose()
        {
            Finish();
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
