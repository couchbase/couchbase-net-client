#nullable enable
using System;
using System.Diagnostics;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    /// <summary>
    /// An implementation of <see cref="IRequestSpan"/> that measures the duration of a span and
    /// is used for providing data for the <see cref="ThresholdLoggingTracer"/>.
    /// requests.
    /// </summary>
    internal class ThresholdRequestSpan : IRequestSpan
    {
        private readonly ThresholdLoggingTracer _tracer;
        private readonly Activity? _activity;
        private readonly IRequestSpan? _parentSpan;

        public ThresholdRequestSpan(ThresholdLoggingTracer tracer, Activity? activity, IRequestSpan? parentSpan = null)
        {
            _tracer = tracer;
            _activity = activity;
            _parentSpan = parentSpan;
            _activity?.SetStartTime(DateTime.UtcNow);
        }

        /// <inheritdoc />
        public IRequestSpan? Parent { get; set; }

        /// <inheritdoc />
        public IRequestSpan ChildSpan(string name)
        {
            return _tracer.RequestSpan(name, this);
        }

        /// <inheritdoc />
        public bool CanWrite { get; } = true;

        /// <inheritdoc />
        public string? Id => _activity?.Id;

        /// <inheritdoc />
        public uint? Duration { get; private set; }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, string value)
        {
            _activity?.AddTag(key, value);
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, uint value)
        {
            _activity?.AddTag(key, value);
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, bool value)
        {
            _activity?.AddTag(key, value);
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
        {
            var activityEvent = new ActivityEvent(name, timestamp?? default);
            _activity?.AddEvent(activityEvent);
            return this;
        }

        /// <inheritdoc />
        public void End()
        {
            _activity?.SetEndTime(DateTime.UtcNow);
            Duration = _activity?.Duration.ToMicroseconds();
            var duration = Duration.ToString();

            if (_activity?.DisplayName != null)
            {
                //total_duration is the root span duration - the others are based on the activity name i.e encoding or dispatch
                SetAttribute(_parentSpan == null ? "total_duration" : $"{_activity.DisplayName}_duration",
                    duration ?? string.Empty);
            }

            _activity?.Stop();

            //This needs to be improved as the parent activity is null for some reason along this code path.
            //For reference a similar code path in ClusterNode produces child activities with parent activities,
            //so something isn't right here, but for the threshold logging this will suffice with a few assumptions
            //i.e. dispatch and encoding spans do not have children.
            if (_parentSpan != null)
            {
                foreach (var activityTag in _activity!.Tags)
                {
                    _parentSpan.SetAttribute(activityTag.Key, activityTag.Value!);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            End();
        }
    }
}
