#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public sealed class ActivityRequestTracer : IRequestTracer, IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly DiagnosticListener _diagnosticListener;

        public ActivityRequestTracer(ILoggerFactory loggerFactory, ClusterOptions options)
        {
            _loggerFactory = loggerFactory;
            _diagnosticListener = new DiagnosticListener(RequestTracing.SourceName);
            _diagnosticListener.Subscribe(new ThresholdActivityObserver(loggerFactory, options.ThresholdOptions ?? new ThresholdOptions()));
        }

        public IInternalSpan InternalSpan(string operationName, IRequestSpan parent)
        {
            var activity = new Activity(operationName);
            if (parent?.Activity == null)
            {
                activity.SetIdFormat(ActivityIdFormat.W3C);
            }


            var span = new ActivitySpan(this, _diagnosticListener, activity, parent);
            _diagnosticListener.StartActivity(activity, span);
            return span;
        }

        public IRequestSpan RequestSpan(string operationName, IRequestSpan parent) =>
            InternalSpan(operationName, parent);

        public static IDisposable Subscribe(Action<Activity?, KeyValuePair<string, object?>> onNext)
            => DiagnosticListener.AllListeners.Subscribe(new SelfSourceObserver(onNext));

        private class SelfSourceObserver : IObserver<DiagnosticListener>, IDisposable
        {
            private readonly Action<Activity?, KeyValuePair<string, object?>> _onNext;
            private IDisposable? _subscription = null;


            public SelfSourceObserver(Action<Activity?, KeyValuePair<string, object?>> onNext)
            {
                _onNext = onNext;
            }

            public void OnCompleted() => Dispose();

            public void OnError(Exception error)
            {
            }

            public void OnNext(DiagnosticListener value)
            {
                if (value.Name == RequestTracing.SourceName)
                {
                    _subscription = value.Subscribe(new ListenerCallbackObserver(_onNext));
                }
            }

            public void Dispose()
            {
                _subscription?.Dispose();
            }
        }

        private class ListenerCallbackObserver : IObserver<KeyValuePair<string, object?>>
        {
            private readonly Action<Activity?, KeyValuePair<string, object?>> _onNext;

            public ListenerCallbackObserver(Action<Activity?, KeyValuePair<string, object?>> onNext) =>
                (_onNext) = onNext;

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(KeyValuePair<string, object?> value) => _onNext(Activity.Current, value);
        }

        public void Dispose()
        {
            _diagnosticListener.Dispose();
        }
    }
}
