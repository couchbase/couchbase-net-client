using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Retry
{
    public abstract class RequestBase : IRequest
    {
        private readonly Stopwatch? _stopwatch = Stopwatch.StartNew();
        private IValueRecorder? _recorder;
        private IRetryStrategy? _retryStrategy;

        public uint Attempts { get; set; }

        public IRetryStrategy RetryStrategy
        {
            get => _retryStrategy ??= new BestEffortRetryStrategy();
            set => _retryStrategy = value;
        }
        public TimeSpan Timeout { get; set; }
        public CancellationToken Token { get; set; }
        public string? ClientContextId { get; set; }
        public abstract  bool Idempotent { get; }
        public List<RetryReason> RetryReasons { get; set; } = new();
        public string? Statement { get; set; }

        #region Tracing and Metrics

        /// <inheritdoc />
        public void StopRecording()
        {
            _stopwatch?.Stop();
            if (_stopwatch?.ElapsedMilliseconds != null)
            {
                _recorder?.RecordValue(_stopwatch.Elapsed.ToMicroseconds());
            }
        }

        /// <inheritdoc />
        public IValueRecorder Recorder
        {
            get => _recorder ?? NoopValueRecorder.Instance;
            set => _recorder = value;
        }

        public void LogOrphaned()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
