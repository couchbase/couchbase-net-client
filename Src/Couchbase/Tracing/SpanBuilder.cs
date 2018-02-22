using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Couchbase.Utils;
using OpenTracing;

namespace Couchbase.Tracing
{
    internal class SpanBuilder : ISpanBuilder
    {
        private static long _traceId;
        private static long _spanId;

        private readonly ThresholdLoggingTracer _tracer;
        private readonly string _operationName;
        private long? _startTimestamp;
        private Span _parentSpan;
        private readonly List<Reference> _references = new List<Reference>();

        private readonly Dictionary<string, object> _tags = new Dictionary<string, object>
        {
            {Tags.Component, ClientIdentifier.GetClientDescription()},
            {Tags.DbType, CouchbaseTags.DbTypeCouchbase},
            {Tags.SpanKind, Tags.SpanKindClient}
        };

        public SpanBuilder(ThresholdLoggingTracer tracer, string operationName)
        {
            _tracer = tracer;
            _operationName = operationName;
        }

        public ISpanBuilder AsChildOf(ISpan parent)
        {
            if (parent != null)
            {
                _references.Add(new Reference(References.ChildOf, parent.Context));
                if (parent is Span span)
                {
                    _parentSpan = span;
                }
            }

            return this;
        }

        public ISpanBuilder AsChildOf(ISpanContext parent)
        {
            _references.Add(new Reference(References.ChildOf, parent));
            return this;
        }

        public ISpanBuilder FollowsFrom(ISpan parent)
        {
            _references.Add(new Reference(References.FollowsFrom, parent.Context));
            return this;
        }

        public ISpanBuilder FollowsFrom(ISpanContext parent)
        {
            _references.Add(new Reference(References.FollowsFrom, parent));
            return this;
        }

        public ISpanBuilder AddReference(string referenceType, ISpanContext referencedContext)
        {
            _references.Add(new Reference(referenceType, referencedContext));
            return this;
        }

        public ISpanBuilder WithTag(string key, bool value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithTag(string key, double value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithTag(string key, int value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithTag(string key, string value)
        {
            _tags.Add(key, value);
            return this;
        }

        public ISpanBuilder WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            _startTimestamp = startTimestamp.Ticks;
            return this;
        }

        public ISpan Start()
        {
            var context = _references.Any() ? CreateChildContext() : CreateNewContext();
            var span = new Span(_tracer, _operationName, context, _startTimestamp ?? Stopwatch.GetTimestamp(), _tags, _references);
            _parentSpan?.Spans.Add(span);

            return span;
        }

        private ISpanContext CreateNewContext()
        {
            var traceId = Interlocked.Increment(ref _traceId);
            var spanId = Interlocked.Increment(ref _spanId);
            return new SpanContext(traceId, spanId, 0, null);
        }

        private ISpanContext CreateChildContext()
        {
            var preferredContext = PrefferedContext();

            long traceId = 0, spanId = 0;
            if (preferredContext is SpanContext context)
            {
                traceId = context.TraceId;
                spanId = context.SpanId;
            }

            return new SpanContext(
                traceId,
                Interlocked.Increment(ref _spanId),
                spanId,
                _references.SelectMany(x => x.Context.GetBaggageItems())
            );
        }

        private ISpanContext PrefferedContext()
        {
            var preferredReference = _references.First();
            foreach (var reference in _references)
            {
                if (reference.Type == References.ChildOf && preferredReference.Type != References.ChildOf)
                {
                    preferredReference = reference;
                    break;
                }
            }

            return preferredReference.Context;
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
