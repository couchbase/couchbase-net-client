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

        public ActivitySpan(ActivityRequestTracer tracer, DiagnosticListener diagListener, Activity? activity)
        {
            _tracer = tracer;
            _diagListener = diagListener;
            Activity = activity;
        }

        public Activity? Activity { get; }

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
            if (Activity?.Parent == null)
            {
                return;
            }

            if (Duration != null)
            {
                var microseconds = Duration.Value.ToMicroseconds().ToString();
                switch (Activity.OperationName)
                {
                    case RequestTracing.DispatchSpanName:
                        Activity.Parent.AddTag(nameof(ThresholdSummary.last_dispatch_us), microseconds);
                        Activity.Parent.AddTag(nameof(ThresholdSummary.dispatch_us), microseconds);
                        break;
                    case RequestTracing.PayloadEncodingSpanName:
                        Activity.Parent.AddTag(nameof(ThresholdSummary.encode_us), microseconds);
                        break;
                }
            }

            foreach (var tag in Activity.Tags)
            {
                switch (tag.Key)
                {
                    case CouchbaseTags.OperationId:
                        Activity.Parent.AddTag(nameof(ThresholdSummary.last_operation_id), tag.Value);
                        break;
                    case CouchbaseTags.LocalAddress:
                        Activity.Parent.AddTag(nameof(ThresholdSummary.last_local_address), tag.Value);
                        break;
                    case CouchbaseTags.RemoteAddress:
                        Activity.Parent.AddTag(nameof(ThresholdSummary.last_remote_address), tag.Value);
                        break;
                    case CouchbaseTags.LocalId:
                        Activity.Parent.AddTag(nameof(ThresholdSummary.last_local_id), tag.Value);
                        break;
                    case CouchbaseTags.PeerLatency:
                        Activity.Parent.AddTag(nameof(ThresholdSummary.server_us), tag.Value);
                        break;
                    case nameof(ThresholdSummary.server_us):
                    case nameof(ThresholdSummary.dispatch_us):
                    case nameof(ThresholdSummary.encode_us):
                        Activity.Parent.AddTag(tag.Key, tag.Value);
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
