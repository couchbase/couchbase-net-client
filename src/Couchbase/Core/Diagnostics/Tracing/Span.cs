using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal class Span : ISpan
    {
        private static long _traceId;

        private static string NextTraceId()
        {
            return Interlocked.Increment(ref _traceId).ToString(CultureInfo.InvariantCulture);
        }

        private static long _spanId;

        private static string NextSpanId()
        {
            return Interlocked.Increment(ref _spanId).ToString(CultureInfo.InvariantCulture);
        }

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private readonly long _startTimestamp;
        private long? _endTimestamp;
        private readonly List<Span> _spans = new List<Span>();

        internal ThresholdLoggingTracer Tracer { get; }
        internal List<Reference> References { get; }

        public string OperationName { get; private set; }
        public ISpanContext Context { get; }
        public Dictionary<string, object> Tags { get; }
        public Dictionary<string, string> Baggage { get; } = new Dictionary<string, string>();

        public List<Span> Spans
        {
            get
            {
                // needs lock to prevent concurrent access
                _lock.Wait();
                try
                {
                    return _spans.ToList();
                }
                finally
                {
                    _lock.Release(1);
                }
            }
        }

        public string ParentId { get; }

        internal Span(ThresholdLoggingTracer tracer, string operationName, ISpanContext parentContext, long startTimestamp, Dictionary<string, object> tags, List<Reference> references)
        {
            Tracer = tracer;
            OperationName = operationName;
            _startTimestamp = startTimestamp;
            Tags = tags ?? new Dictionary<string, object>();
            References = references ?? new List<Reference>();

            if (parentContext == null)
            {
                // new root span
                ParentId = null;
                Context = new SpanContext(NextTraceId(), NextSpanId(), null);
            }
            else
            {
                // sub-span
                ParentId = parentContext.SpanId;
                Context = new SpanContext(
                    parentContext.TraceId,
                    NextSpanId(),
                    References.SelectMany(x => x.Context.GetBaggageItems())
                );
            }
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
            Tracer.ReportSpan(this);
        }

        public void Finish(DateTimeOffset finishTimestamp)
        {
            if (_endTimestamp.HasValue)
            {
                // span has already been stopped ..
                return;
            }

            _endTimestamp = finishTimestamp.Ticks;
            Tracer.ReportSpan(this);
        }

        public void Dispose()
        {
            Finish();
        }

        public void AddSpan(Span span)
        {
            _lock.Wait();
            try
            {
                if (_endTimestamp.HasValue)
                {
                    // span has already finsihed, can't add more sub-spans
                    return;
                }

                _spans.Add(span);
            }
            finally
            {
                _lock.Release(1);
            }
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
