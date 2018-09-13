using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTracing;
using OpenTracing.Tag;

namespace Couchbase.Tracing
{
    internal class SpanBuilder : ISpanBuilder
    {
        private readonly ThresholdLoggingTracer _tracer;
        private readonly string _operationName;
        private long? _startTimestamp;
        private Span _parentSpan;
        private readonly List<Reference> _references = new List<Reference>();
        private readonly Dictionary<string, object> _tags = new Dictionary<string, object>();

        private bool _ignoreActiveSpan;

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
                _parentSpan = (Span) parent;
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

        public ISpanBuilder IgnoreActiveSpan()
        {
            _ignoreActiveSpan = true;
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

        public ISpanBuilder WithTag(BooleanTag tag, bool value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpanBuilder WithTag(IntOrStringTag tag, string value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpanBuilder WithTag(IntTag tag, int value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpanBuilder WithTag(StringTag tag, string value)
        {
            _tags.Add(tag.Key, value);
            return this;
        }

        public ISpanBuilder WithStartTimestamp(DateTimeOffset startTimestamp)
        {
            _startTimestamp = startTimestamp.Ticks;
            return this;
        }

        public IScope StartActive()
        {
            return StartActive(true);
        }

        public IScope StartActive(bool finishSpanOnDispose)
        {
            var span = Start();
            return _tracer.ScopeManager.Activate(span, finishSpanOnDispose);
        }

        public ISpan Start()
        {
            ISpanContext activeSpanContext = null;
            if (!_references.Any() && !_ignoreActiveSpan && _tracer.ActiveSpan?.Context != null)
            {
                activeSpanContext = _tracer.ActiveSpan?.Context;
                _references.Add(new Reference(References.ChildOf, _tracer.ActiveSpan?.Context));
            }

            var span = new Span(_tracer, _operationName, activeSpanContext, _startTimestamp ?? Stopwatch.GetTimestamp(), _tags, _references);
            _parentSpan?.AddSpan(span);

            return span;
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
