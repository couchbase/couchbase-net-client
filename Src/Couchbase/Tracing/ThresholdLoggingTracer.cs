using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTracing;
using OpenTracing.Mock;
using OpenTracing.Propagation;
using OpenTracing.Util;

namespace Couchbase.Tracing
{
    public class ThresholdLoggingTracer : ITracer, IDisposable
    {
        private const int WorkerSleep = 100;
        private static readonly ILog Log = LogManager.GetLogger<ThresholdLoggingTracer>();

        private readonly CancellationTokenSource _source = new CancellationTokenSource();
        private readonly BlockingCollection<SpanSummary> _queue = new BlockingCollection<SpanSummary>(1000);
        private readonly SortedSet<SpanSummary> _kvSummaries = new SortedSet<SpanSummary>();
        private readonly SortedSet<SpanSummary> _viewSummaries = new SortedSet<SpanSummary>();
        private readonly SortedSet<SpanSummary> _querySummaries = new SortedSet<SpanSummary>();
        private readonly SortedSet<SpanSummary> _searchSummaries = new SortedSet<SpanSummary>();
        private readonly SortedSet<SpanSummary> _analyticsSummaries = new SortedSet<SpanSummary>();

        private DateTime _lastrun = DateTime.UtcNow;
        private int _kvSummaryCount;
        private int _viewSummaryCount;
        private int _querySummaryCount;
        private int _searchSummaryCount;
        private int _analyticsSummaryCount;
        private bool _hasSummariesToLog;

        /// <summary>
        /// Gets or sets the interval at which the <see cref="ThresholdLoggingTracer"/> writes to the log.
        /// Expressed as milliseconds.
        /// </summary>
        public int Interval { get; set; } = 10000; // 10 seconds

        /// <summary>
        /// Gets or sets the size of the sample used in the output written to the log.
        /// </summary>
        public int SampleSize { get; set; } = 10;

        /// <summary>
        /// Gets or sets the key-value operation threshold, expressed in microseconds.
        /// </summary>
        public int KvThreshold { get; set; } = 500000;

        /// <summary>
        /// Gets or sets the view operation threshold, expressed in microseconds.
        /// </summary>
        public int ViewThreshold { get; set; } = 1000000;

        /// <summary>
        /// Gets or sets the n1ql operation threshold, expressed in microseconds.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public int N1qlThreshold { get; set; } = 1000000;

        /// <summary>
        /// Gets or sets the search operation threshold, expressed in microseconds.
        /// </summary>
        public int SearchThreshold { get; set; } = 1000000;

        /// <summary>
        /// Gets or sets the analytics operation threshold, expressed in microseconds.
        /// </summary>
        public int AnalyticsThreshold { get; set; } = 1000000;

        /// <summary>
        /// Internal total count of all pending spans that have exceed the given service thresholds.
        /// </summary>
        internal int TotalSummaryCount => _kvSummaryCount + _viewSummaryCount + _querySummaryCount + _searchSummaryCount + _analyticsSummaryCount;

        public IScopeManager ScopeManager { get; } = new AsyncLocalScopeManager();

        public ISpan ActiveSpan => ScopeManager?.Active?.Span;

        public ThresholdLoggingTracer()
        {
            Task.Factory.StartNew(DoWork, TaskCreationOptions.LongRunning);
        }

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new SpanBuilder(this, operationName);
        }

        public void Inject<TCarrier>(ISpanContext spanContext, IFormat<TCarrier> format, TCarrier carrier)
        {
            throw new NotSupportedException();
        }

        public ISpanContext Extract<TCarrier>(IFormat<TCarrier> format, TCarrier carrier)
        {
            throw new NotSupportedException();
        }

        internal void ReportSpan(Span span)
        {
            if (span.IsRootSpan && !span.ContainsIgnore)
            {
                var summary = new SpanSummary(span);
                if (IsOverThreshold(summary))
                {
                    _queue.Add(summary);
                }
            }
        }

        private bool IsOverThreshold(SpanSummary summary)
        {
            switch (summary.ServiceType)
            {
                case CouchbaseTags.ServiceKv:
                    return summary.TotalDuration > KvThreshold;
                case CouchbaseTags.ServiceView:
                    return summary.TotalDuration > ViewThreshold;
                case CouchbaseTags.ServiceQuery:
                    return summary.TotalDuration > N1qlThreshold;
                case CouchbaseTags.ServiceSearch:
                    return summary.TotalDuration > SearchThreshold;
                case CouchbaseTags.ServiceAnalytics:
                    return summary.TotalDuration > AnalyticsThreshold;
                default:
                    return false;
            }
        }

        private async Task DoWork()
        {
            while (!_source.Token.IsCancellationRequested)
            {
                try
                {
                    // determine if we need to write to log yet
                    if (DateTime.UtcNow.Subtract(_lastrun) > TimeSpan.FromMilliseconds(Interval))
                    {
                        if (_hasSummariesToLog)
                        {
                            var result = new JArray();
                            AddSummariesToResult(result, CouchbaseTags.ServiceKv, _kvSummaries, ref _kvSummaryCount);
                            AddSummariesToResult(result, CouchbaseTags.ServiceView, _viewSummaries, ref _viewSummaryCount);
                            AddSummariesToResult(result, CouchbaseTags.ServiceQuery, _querySummaries, ref _querySummaryCount);
                            AddSummariesToResult(result, CouchbaseTags.ServiceSearch, _searchSummaries, ref _searchSummaryCount);
                            AddSummariesToResult(result, CouchbaseTags.ServiceAnalytics, _analyticsSummaries, ref _analyticsSummaryCount);

                            Log.Info("Operations that exceeded service threshold: {0}", result.ToString(Formatting.None));

                            _hasSummariesToLog = false;
                        }

                        _lastrun = DateTime.UtcNow;
                    }

                    while (_queue.TryTake(out var summary, WorkerSleep, _source.Token))
                    {
                        if (_source.IsCancellationRequested)
                        {
                            break;
                        }

                        switch (summary.ServiceType)
                        {
                            case CouchbaseTags.ServiceKv:
                                AddSummryToSet(_kvSummaries, summary, ref _kvSummaryCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceView:
                                AddSummryToSet(_viewSummaries, summary, ref _viewSummaryCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceQuery:
                                AddSummryToSet(_querySummaries, summary, ref _querySummaryCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceSearch:
                                AddSummryToSet(_searchSummaries, summary, ref _searchSummaryCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceAnalytics:
                                AddSummryToSet(_analyticsSummaries, summary, ref _analyticsSummaryCount, SampleSize);
                                break;
                            default:
                                Log.Info($"Unknown service type {summary.ServiceType}");
                                break;
                        }

                        _hasSummariesToLog = true; // indicates we have something to process
                    }

                    // sleep for a little while
                    await Task.Delay(TimeSpan.FromMilliseconds(WorkerSleep), _source.Token).ContinueOnAnyContext();
                }
                catch (ObjectDisposedException) { } // ignore
                catch (OperationCanceledException) { } // ignore
                catch (Exception exception)
                {
                    Log.Error("Error when procesing spans for spans over serivce thresholds", exception);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(WorkerSleep), _source.Token).ContinueOnAnyContext();
            }
        }

        private static void AddSummariesToResult(JArray result, string serviceName, ICollection<SpanSummary> summaries, ref int summaryCount)
        {
            if (summaries.Any())
            {
                result.Add(new JObject
                {
                    {"service", serviceName},
                    {"count", summaryCount},
                    {"top", JArray.FromObject(summaries)}
                });
            }

            summaries.Clear();
            summaryCount = 0;
        }

        private static void AddSummryToSet(ICollection<SpanSummary> summaries, SpanSummary summary, ref int summaryCount, int maxSampleSize)
        {
            summaries.Add(summary);
            summaryCount += 1;

            while (summaries.Count > maxSampleSize)
            {
                summaries.Remove(summaries.First());
            }
        }

        public void Dispose()
        {
            _source?.Cancel();

            if (_queue != null)
            {
                _queue.CompleteAdding();
                while (_queue.Any())
                {
                    _queue.TryTake(out _);
                }

                _queue.Dispose();
            }
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
