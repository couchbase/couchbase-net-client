using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Utils;
using Newtonsoft.Json;
using OpenTracing;
using OpenTracing.Propagation;

namespace Couchbase.Tracing
{
    public class ThresholdLoggingTracer : ITracer, IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<ThresholdLoggingTracer>();
        internal const int MaxQueueCapacity = 1024;

        private readonly CancellationTokenSource _source = new CancellationTokenSource();
        private readonly LimitedConcurrentQueue<Span> _queuedSpans = new LimitedConcurrentQueue<Span>(MaxQueueCapacity);

        internal int Interval { get; } = 10000;
        internal int SampleSize { get; } = 10;

        internal Dictionary<string, int> ServiceFloors { get; } = new Dictionary<string, int>
        {
            {CouchbaseTags.ServiceKv, 500000}, // 500 milliseconds
            {CouchbaseTags.ServiceView, 1000000}, // 1 second
            {CouchbaseTags.ServiceN1ql, 1000000}, // 1 second
            {CouchbaseTags.ServiceSearch, 1000000}, // 1 second
            {CouchbaseTags.ServiceAnalytics, 1000000} // 1 second
        };

        internal int QueuedSpansCount => _queuedSpans.Count;

        public ThresholdLoggingTracer(int interval, int sampleSize, Dictionary<string, int> serviceFloors)
            : this()
        {
            Interval = interval;
            SampleSize = sampleSize;
            ServiceFloors = serviceFloors;
        }

        internal ThresholdLoggingTracer()
        {
            Task.Factory.StartNew(DoWork, TaskCreationOptions.LongRunning);
        }

        public ISpanBuilder BuildSpan(string operationName)
        {
            return new SpanBuilder(this, operationName);
        }

        public void Inject<TCarrier>(ISpanContext spanContext, Format<TCarrier> format, TCarrier carrier)
        {
            throw new NotSupportedException();
        }

        public ISpanContext Extract<TCarrier>(Format<TCarrier> format, TCarrier carrier)
        {
            throw new NotSupportedException();
        }

        public int KvThreshold
        {
            get => ServiceFloors[CouchbaseTags.ServiceKv];
            set => ServiceFloors[CouchbaseTags.ServiceKv] = value;
        }

        public int ViewThreshold
        {
            get => ServiceFloors[CouchbaseTags.ServiceView];
            set => ServiceFloors[CouchbaseTags.ServiceView] = value;
        }

        // ReSharper disable once InconsistentNaming
        public int N1qlThreshold
        {
            get => ServiceFloors[CouchbaseTags.ServiceN1ql];
            set => ServiceFloors[CouchbaseTags.ServiceN1ql] = value;
        }

        public int SearchThreshold
        {
            get => ServiceFloors[CouchbaseTags.ServiceSearch];
            set => ServiceFloors[CouchbaseTags.ServiceSearch] = value;
        }

        public int AnalyticsThreshold
        {
            get => ServiceFloors[CouchbaseTags.ServiceAnalytics];
            set => ServiceFloors[CouchbaseTags.ServiceAnalytics] = value;
        }

        internal void ReportSpan(Span span)
        {
            if (ShouldQueueSpan(span))
            {
                //TODO: create summary here to reduce memory consumption?
                _queuedSpans.Enqueue(span);
            }
        }

        private static bool ShouldQueueSpan(Span span)
        {
            return span.IsRootSpan &&
                   !span.ContainsIgnore &&
                   span.ContainsService;
        }

        private async Task DoWork()
        {
            while (!_source.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Interval), _source.Token);
                if (_queuedSpans.IsEmpty)
                {
                    Log.Trace("No spans to process.");
                    continue;
                }

                Log.Trace("Processing {0} spans.", _queuedSpans.Count);

                // dequeue spans into local collection
                var spans = new List<Span>();
                while (_queuedSpans.TryDequeue(out var span))
                {
                    spans.Add(span);
                }

                // group by service name
                // get floor and filter
                // order by duration
                // take sample size
                // convert span into summary
                var serviceSummaries = spans
                    .GroupBy(GetServiceName)
                    .Select(group =>
                    {
                        var floor = GetFloor(group.Key);
                        return new
                        {
                            service = group.Key,
                            spans = group.Where(span => floor > 0 && span.Duration >= floor)
                        };
                    })
                    .Where(group => group.spans.Any())
                    .Select(group =>
                    {
                        return new
                        {
                            group.service,
                            count = group.spans.Count(),
                            top = group.spans.OrderByDescending(span => span.Duration)
                                .Take(SampleSize)
                                .Select(span => new SpanSummary(span))
                        };
                    });

                if (serviceSummaries.Any())
                {
                    Log.Info("Operations that exceeded service threshold: {0}", JsonConvert.SerializeObject(serviceSummaries, Formatting.None));
                }
            }
        }

        private static string GetServiceName(Span span)
        {
            return span.Tags.TryGetValue(CouchbaseTags.Service, out var serviceName) ? (string) serviceName : "unknown";
        }

        private int GetFloor(string serviceName)
        {
            return ServiceFloors.TryGetValue(serviceName, out var floor) ? floor : 0;
        }

        public void Dispose()
        {
            _source?.Cancel();
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
