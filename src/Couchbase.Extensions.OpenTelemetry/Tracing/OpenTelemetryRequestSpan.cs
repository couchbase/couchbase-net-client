#nullable enable
using System;
using System.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;

namespace Couchbase.Extensions.Tracing.Otel.Tracing
{
    internal class OpenTelemetryRequestSpan : IRequestSpan
    {
        // Avoid re-boxing booleans on the heap when setting attributes
        private static readonly object TrueBoxed = true;
        private static readonly object FalseBoxed = false;

        private readonly IRequestTracer _tracer;
        private readonly Activity _activity;
        private readonly IRequestSpan? _parentSpan;

        public OpenTelemetryRequestSpan(IRequestTracer tracer, Activity activity, IRequestSpan? parentSpan = null)
        {
            _tracer = tracer;
            _activity = activity;
            _parentSpan = parentSpan;
        }

        /// <inheritdoc />
        public bool CanWrite => true;

        /// <inheritdoc />
        public string? Id => _activity.Id;

        public uint? Duration { get; private set; }

        /// <inheritdoc />
        public IRequestSpan? Parent
        {
            get => _parentSpan;
            set => throw new NotSupportedException("OpenTelemetry tracing does not support setting the parent on an existing span.");
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, string value)
        {
            _activity.AddTag(key, value);
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, uint value)
        {
            _activity.AddTag(key, value);
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, bool value)
        {
            _activity.AddTag(key, value ? TrueBoxed : FalseBoxed);
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
        {
            var activityEvent = new ActivityEvent(name, timestamp ?? default);
            _activity.AddEvent(activityEvent);
            return this;
        }

        public IRequestSpan ChildSpan(string name)
        {
            return _tracer.RequestSpan(name, this);
        }

        public void End()
        {
            _activity.Stop();
        }

        public void Dispose()
        {
            End();
        }

    }
}
