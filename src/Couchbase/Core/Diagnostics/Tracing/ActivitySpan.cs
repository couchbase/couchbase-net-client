#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal sealed class ActivitySpan : IInternalSpan
    {
        private readonly ActivityRequestTracer _tracer;
        private readonly DiagnosticListener _diagListener;

        public ActivitySpan(ActivityRequestTracer tracer, DiagnosticListener diagListener, Activity? activity, IRequestSpan? parentSpan)
        {
            _tracer = tracer;
            _diagListener = diagListener;
            Activity = activity;
            ParentSpan = parentSpan;
        }

        public Activity? Activity { get; }

        /// <summary>
        /// Parent span, if any. Otherwise, null.
        /// </summary>
        /// <remarks>
        /// Used to propagate tags upwards to only Couchbase related spans. The parent of <see cref="Activity"/> may
        /// be a span that doesn't belong to this SDK.
        /// </remarks>
        public IRequestSpan? ParentSpan { get; }

        /// <inheritdoc />
        public bool IsNullSpan => false;

        public void Dispose()
        {
            if (Activity == null)
            {
                return;
            }

            PropagateTagsUpwards();
            _diagListener.StopActivity(Activity, this);
        }

        private void PropagateTagsUpwards()
        {
            if (Activity == null)
            {
                return;
            }

            var parent = ParentSpan?.Activity;
            if (parent == null)
            {
                return;
            }

            if (Duration != null)
            {
                var microseconds = Duration.Value.ToMicroseconds().ToString();
                switch (Activity.OperationName)
                {
                    case RequestTracing.DispatchSpanName:
                        parent.AddTag(nameof(ThresholdSummary.last_dispatch_us), microseconds);
                        parent.AddTag(nameof(ThresholdSummary.dispatch_us), microseconds);
                        break;
                    case RequestTracing.PayloadEncodingSpanName:
                        parent.AddTag(nameof(ThresholdSummary.encode_us), microseconds);
                        break;
                }
            }

            foreach (var tag in Activity.Tags)
            {
                switch (tag.Key)
                {
                    case CouchbaseTags.OperationId:
                        parent.AddTag(nameof(ThresholdSummary.last_operation_id), tag.Value);
                        break;
                    case CouchbaseTags.LocalAddress:
                        parent.AddTag(nameof(ThresholdSummary.last_local_address), tag.Value);
                        break;
                    case CouchbaseTags.RemoteAddress:
                        parent.AddTag(nameof(ThresholdSummary.last_remote_address), tag.Value);
                        break;
                    case CouchbaseTags.LocalId:
                        parent.AddTag(nameof(ThresholdSummary.last_local_id), tag.Value);
                        break;
                    case CouchbaseTags.PeerLatency:
                        parent.AddTag(nameof(ThresholdSummary.server_us), tag.Value);
                        break;
                    case nameof(ThresholdSummary.server_us):
                    case nameof(ThresholdSummary.dispatch_us):
                    case nameof(ThresholdSummary.encode_us):
                        parent.AddTag(tag.Key, tag.Value);
                        break;
                }
            }
        }

        public IInternalSpan StartPayloadEncoding() => StartChild(RequestTracing.PayloadEncodingSpanName);

        public IInternalSpan StartDispatch() => StartChild(RequestTracing.DispatchSpanName);

        public IInternalSpan WithTag(string key, string value)
        {
            Activity?.AddTag(key, value);
            return this;
        }

        public TimeSpan? Duration => Activity?.Duration;

        private IInternalSpan StartChild(string operationName) => _tracer.InternalSpan(operationName, this);
    }
}
