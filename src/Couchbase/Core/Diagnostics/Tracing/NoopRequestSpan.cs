#nullable enable
using System;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A NOOP implementation of <see cref="IRequestSpan"/>. Calling any method will do nothing as it's a NOOP,
    /// except for constructing child spans. Tracers may opt to return a <see cref="NoopRequestSpan"/> when disabled
    /// or when sampling excludes the span, but a tracer may still choose to create an active span for the child
    /// span of a NOOP span.
    /// </summary>
    // We seal this for a minor perf gain at sites which use "span is NoopRequestSpan", if the type is sealed
    // it will be a simple equality comparison on the type code rather than walking the inheritance hierarchy.
    public sealed class NoopRequestSpan : IRequestSpan
    {
        public static readonly IRequestSpan Instance = new NoopRequestSpan();

        private readonly IRequestTracer? _tracer;
        private readonly IRequestSpan? _parentSpan;

        /// <summary>
        /// Creates a new NoopRequestSpan.
        /// </summary>
        public NoopRequestSpan() : this(null)
        {
        }

        /// <summary>
        /// Creates a new NoopRequestSpan.
        /// </summary>
        /// <param name="tracer">The <see cref="IRequestTracer"/> used constructing child spans.</param>
        /// <param name="parentSpan">The <see cref="IRequestSpan"/> which is the parent of this span, if any.</param>
        public NoopRequestSpan(IRequestTracer? tracer, IRequestSpan? parentSpan = null)
        {
            _tracer = tracer;
            _parentSpan = parentSpan;
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, bool value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, string value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, uint value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
        {
            return this;
        }

        /// <inheritdoc />
        public void End()
        {
        }

        /// <inheritdoc />
        public IRequestSpan? Parent
        {
            get => _parentSpan;
            // ReSharper disable once ValueParameterNotUsed
            set => ThrowHelper.ThrowNotSupportedException("Cannot set the parent on a NoopRequestSpan.");
        }

        /// <inheritdoc />
        public IRequestSpan ChildSpan(string name) =>
            _tracer != null
                ? _tracer.RequestSpan(name, this)
                : this;

        /// <inheritdoc />
        public bool CanWrite => false;

        /// <inheritdoc />
        public string? Id => null;

        /// <inheritdoc />
        public uint? Duration => null;
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
