#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.Utils;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// An implementation of <see cref="IRequestSpan"/> that measures the duration of a span and
    /// is used for providing data for the <see cref="RequestTracer"/>.
    /// requests.
    /// </summary>
    // We seal this for a minor perf gain at sites which use "span is RequestSpan", if the type is sealed
    // it will be a simple equality comparison on the type code rather than walking the inheritance hierarchy.
    internal sealed class RequestSpan : IRequestSpan
    {
        // Avoid re-boxing booleans on the heap when setting attributes
        private static readonly object TrueBoxed = true;
        private static readonly object FalseBoxed = false;

        private readonly IRequestTracer _tracer;
        private readonly Activity _activity;
        private readonly IRequestSpan? _parentSpan;
        private volatile bool _disposed;

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

        public ActivityContext ActivityContext => _activity.Context;

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

        /// <inheritdoc />
        public IRequestSpan SetStatus(RequestSpanStatusCode code)
        {
            _activity.SetStatus(code switch
            {
                RequestSpanStatusCode.Ok => ActivityStatusCode.Ok,
                RequestSpanStatusCode.Error => ActivityStatusCode.Error,
                _ => ActivityStatusCode.Unset
            });
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string SetEndTimeAndDuration()
        {
            _activity.SetEndTime(DateTime.UtcNow);

            var duration = _activity.Duration.ToMicroseconds();
            Duration = duration;
            return duration.ToStringInvariant();
        }

        /// <inheritdoc />
        public void End()
        {
            if (_disposed) return;//ensure this is only called once
            if (_parentSpan == null)
            {
                // This is the outer span
                var durationStr = SetEndTimeAndDuration();

                SetAttribute(ThresholdTags.TotalDurationTag, durationStr);

                _activity.Stop();
                _disposed = true;
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

                        SetAttribute(ThresholdTags.EncodeDurationTag, durationStr);

                        _activity.Stop();
                        break;
                    }

                    case InnerRequestSpans.DispatchSpan.Name:
                    {
                        var durationStr = SetEndTimeAndDuration();

                        SetAttribute(ThresholdTags.DispatchDurationTag, durationStr);

                        _activity.Stop();
                        break;
                    }

                    default:
                        // We can be faster by avoiding the duration tagging for other types of spans
                        _activity.Stop();
                        Duration = _activity.Duration.ToMicroseconds();
                        break;
                }

                _disposed = true;
            }

            // The threshold and orphan logs read the encode and dispatch details from the request span, so an
            // encoding or dispatch span copies those tags to its parent. No other span copies tags: a span that
            // copied all its tags would put its own operation name, service and statement on its parent. For
            // example, get_collection_id would rename the operation it runs under in the threshold log.
            if (_activity.OperationName is not (InnerRequestSpans.EncodingSpan.Name or InnerRequestSpans.DispatchSpan.Name))
            {
                return;
            }

            var parentSpan = _parentSpan;
            while (true)
            {
                if (parentSpan is RequestSpanWrapper wrapper)
                {
                    // The tags already have their final names. Through the wrapper, the semantic convention would map
                    // them again.
                    parentSpan = wrapper.InnerSpan;
                }
                else if (parentSpan is NoopRequestSpan noopSpan)
                {
                    // If the parent is a NoopRequestSpan we must pass to its parent. This may happen if one of the inner
                    // spans is not sampled but a deeper span is sampled.
                    parentSpan = noopSpan.Parent;
                }
                else
                {
                    break;
                }
            }
            if (parentSpan != null)
            {
                foreach (var activityTag in _activity.Tags)
                {
                    if (IsCopiedToParent(activityTag.Key))
                    {
                        parentSpan.SetAttribute(activityTag.Key, activityTag.Value!);
                    }
                }
            }
        }

        /// <summary>
        /// The encoding and dispatch tags that <see cref="ThresholdSummary"/> and
        /// <see cref="OrphanResponseReporting.OrphanSummary"/> read from the request span.
        /// </summary>
        private static bool IsCopiedToParent(string key) =>
            key is ThresholdTags.EncodeDurationTag
                or ThresholdTags.DispatchDurationTag
                or InnerRequestSpans.DispatchSpan.Attributes.ServerDuration
                or InnerRequestSpans.DispatchSpan.Attributes.TimeoutMilliseconds
                or InnerRequestSpans.DispatchSpan.Attributes.OperationId
                or InnerRequestSpans.DispatchSpan.Attributes.LocalId
                or InnerRequestSpans.DispatchSpan.Attributes.LocalHostname
                or InnerRequestSpans.DispatchSpan.Attributes.LocalPort
                or InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname
                or InnerRequestSpans.DispatchSpan.Attributes.RemotePort;

        /// <inheritdoc />
        public void Dispose()
        {
            End();//disposed logic is forwarded to this method
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
