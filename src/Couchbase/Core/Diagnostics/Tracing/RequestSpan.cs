#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// An implementation of <see cref="IRequestSpan"/> that measures the duration of a span and
    /// is used for providing data for the <see cref="RequestTracer"/>.
    /// requests.
    /// </summary>
    internal class RequestSpan : IRequestSpan
    {
        // Avoid re-boxing booleans on the heap when setting attributes
        private static readonly object TrueBoxed = true;
        private static readonly object FalseBoxed = false;

        private readonly IRequestTracer _tracer;
        private readonly Activity _activity;
        private readonly IRequestSpan? _parentSpan;

        public RequestSpan(IRequestTracer tracer, Activity activity, IRequestSpan? parentSpan = null)
        {
            _tracer = tracer;
            _activity = activity;
            _parentSpan = parentSpan;
        }

        /// <inheritdoc />
        public IRequestSpan? Parent
        {
            get => _parentSpan;
            // ReSharper disable once ValueParameterNotUsed
            set => ThrowHelper.ThrowNotSupportedException("Cannot set the parent on a RequestSpan.");
        }

        /// <inheritdoc />
        public IRequestSpan ChildSpan(string name)
        {
            return _tracer.RequestSpan(name, this);
        }

        /// <inheritdoc />
        public bool CanWrite => true;

        /// <inheritdoc />
        public string? Id => _activity.Id;

        /// <inheritdoc />
        public uint? Duration { get; private set; }

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
            var activityEvent = new ActivityEvent(name, timestamp?? default);
            _activity.AddEvent(activityEvent);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string SetEndTimeAndDuration()
        {
            _activity.SetEndTime(DateTime.UtcNow);

            var duration = _activity.Duration.ToMicroseconds();
            Duration = duration;
            return duration.ToString();
        }

        /// <inheritdoc />
        public void End()
        {
            if (_parentSpan == null)
            {
                // This is the outer span
                var durationStr = SetEndTimeAndDuration();

                SetAttribute(ThresholdTags.TotalDuration, durationStr);

                _activity.Stop();
            }
            else
            {
                switch (_activity.OperationName)
                {
                    // Check for specific operation names to avoid the tagging for other types of spans
                    // and we can also use a prebuilt constant string instead of building a string each time.
                    case InnerRequestSpans.EncodingSpan.Name:
                    {
                        var durationStr = SetEndTimeAndDuration();

                        SetAttribute(ThresholdTags.EncodeDuration, durationStr);

                        _activity.Stop();
                        break;
                    }

                    case InnerRequestSpans.DispatchSpan.Name:
                    {
                        var durationStr = SetEndTimeAndDuration();

                        SetAttribute(ThresholdTags.DispatchDuration, durationStr);

                        _activity.Stop();
                        break;
                    }

                    default:
                        // We can be faster by avoiding the duration tagging for other types of spans
                        _activity.Stop();
                        Duration = _activity.Duration.ToMicroseconds();
                        break;
                }
            }

            //This needs to be improved as the parent activity is null for some reason along this code path.
            //For reference a similar code path in ClusterNode produces child activities with parent activities,
            //so something isn't right here, but for the threshold logging this will suffice with a few assumptions
            //i.e. dispatch and encoding spans do not have children.
            var parentSpan = _parentSpan;
            if (parentSpan is NoopRequestSpan noopSpan)
            {
                // If the parent is a NoopRequestSpan we must pass to its parent. This may happen if one of the inner
                // spans is not sampled but a deeper span is sampled.
                // Since we must check the type anyway, use the strongly-typed variable to get the parent so that it may be inlined.
                parentSpan = noopSpan.Parent;
            }
            if (parentSpan != null)
            {
                foreach (var activityTag in _activity.Tags)
                {
                    parentSpan.SetAttribute(activityTag.Key, activityTag.Value!);
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
