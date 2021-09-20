using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    internal class OrphanReporter : IOrphanReporter
    {
        private readonly ILogger<OrphanReporter> _logger;
        private const int WorkerSleep = 100;

        private readonly CancellationTokenSource _source = new();

        private readonly Channel<OrphanSummary> _queue =
            Channel.CreateBounded<OrphanSummary>(new BoundedChannelOptions(1000)
            {
                // We drop orphans from the logs when we're full, rather than blocking the calling thread
                // Since we're constantly processing the queue and have room for 1000, this shouldn't
                // occur except in the most extreme scenarios.
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true
            });

        private readonly List<OrphanSummary> _kvOrphans = new();
        private readonly List<OrphanSummary> _viewOrphans = new();
        private readonly List<OrphanSummary> _queryOrphans = new();
        private readonly List<OrphanSummary> _searchOrphans = new();
        private readonly List<OrphanSummary> _analyticsOrphans = new();

        public int Interval { get; set; }
        public uint SampleSize { get; set; }

        private DateTime _lastRun = DateTime.UtcNow;
        private uint _kvOrphanCount;
        private uint _viewOrphanCount;
        private uint _queryOrphanCount;
        private uint _searchOrphanCount;
        private uint _analyticsOrphanCount;
        private bool _hasOrphans;

        /// <summary>
        /// Internal total count of all pending operation contexts to have been recorded.
        /// </summary>
        internal uint TotalCount => _kvOrphanCount + _viewOrphanCount + _queryOrphanCount + _searchOrphanCount + _analyticsOrphanCount;

        public OrphanReporter(ILogger<OrphanReporter> logger, OrphanOptions options)
        {
            _logger = logger;
            Interval = (int)options.EmitInterval.TotalMilliseconds;
            SampleSize = options.SampleSize;

            // Ensure that we don't flow the ExecutionContext into the long running task
            using (ExecutionContext.SuppressFlow())
            {
                Task.Run(DoWork);
            }
        }

        private async Task DoWork()
        {
            var reader = _queue.Reader;

            while (!_source.IsCancellationRequested)
            {
                try
                {
                    // determine if we need to write to log yet
                    if (DateTime.UtcNow.Subtract(_lastRun) > TimeSpan.FromMilliseconds(Interval))
                    {
                        if (_hasOrphans)
                        {
                            var result = new JObject();
                            AddServiceToResult(result, OuterRequestSpans.ServiceSpan.Kv.Name, _kvOrphans, ref _kvOrphanCount);
                            AddServiceToResult(result, OuterRequestSpans.ServiceSpan.ViewQuery, _viewOrphans, ref _viewOrphanCount);
                            AddServiceToResult(result, OuterRequestSpans.ServiceSpan.N1QLQuery, _queryOrphans, ref _queryOrphanCount);
                            AddServiceToResult(result, OuterRequestSpans.ServiceSpan.SearchQuery, _searchOrphans, ref _searchOrphanCount);
                            AddServiceToResult(result, OuterRequestSpans.ServiceSpan.AnalyticsQuery, _analyticsOrphans, ref _analyticsOrphanCount);

                            if (result.HasValues)
                            {
                                _logger.LogWarning("Orphaned responses observed: {0}", result.ToString(Formatting.None));
                            }

                            _hasOrphans = false;
                        }

                        _lastRun = DateTime.UtcNow;
                    }

                    var hasData = true;
                    // Repeat until _source is cancelled or we have no more data AND have waited at least WorkerSleep
                    while (hasData && !_source.IsCancellationRequested)
                    {
                        using (var timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(_source.Token))
                        {
                            timeoutToken.CancelAfter(WorkerSleep);

                            try
                            {
                                hasData = await reader.WaitToReadAsync(timeoutToken.Token).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            {
                                // The timeoutToken was triggered
                                hasData = false;
                            }
                        }

                        // hasData will be false if either the channel is closed or the timeoutToken triggered
                        if (hasData)
                        {
                            // Keep taking data until we run out or _source is cancelled
                            while (!_source.IsCancellationRequested && reader.TryRead(out var context))
                            {
                                switch (context.ServiceType)
                                {
                                    case OuterRequestSpans.ServiceSpan.Kv.Name:
                                        AddContextToService(_kvOrphans, context, ref _kvOrphanCount, SampleSize);
                                        break;
                                    case OuterRequestSpans.ServiceSpan.ViewQuery:
                                        AddContextToService(_viewOrphans, context, ref _viewOrphanCount, SampleSize);
                                        break;
                                    case OuterRequestSpans.ServiceSpan.N1QLQuery:
                                        AddContextToService(_queryOrphans, context, ref _queryOrphanCount, SampleSize);
                                        break;
                                    case OuterRequestSpans.ServiceSpan.SearchQuery:
                                        AddContextToService(_searchOrphans, context, ref _searchOrphanCount,
                                            SampleSize);
                                        break;
                                    case OuterRequestSpans.ServiceSpan.AnalyticsQuery:
                                        AddContextToService(_analyticsOrphans, context, ref _analyticsOrphanCount,
                                            SampleSize);
                                        break;
                                    default:
                                        _logger.LogInformation(
                                            $"Unknown service type {context.ServiceType} for operation with ID '{context.operation_id}'");
                                        break;
                                }

                                _hasOrphans = true; // indicates we have something to process
                            }
                        }
                    }

                    // sleep for a little while
                    await Task.Delay(TimeSpan.FromMilliseconds(WorkerSleep), _source.Token).ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { } // ignore
                catch (OperationCanceledException) { } // ignore
                catch (Exception exception)
                {
                    _logger.LogError("Error when processing Orphaned Responses", exception);
                }
            }
        }

        private static JProperty CreateOrphanJson(string serviceType, ICollection<OrphanSummary> set, uint totalCount)
        {
            return new(serviceType,
                new JObject(new JProperty("total_count", totalCount),
                    new JProperty("top_requests", JArray.FromObject(set))));
        }

        private static void AddContextToService(ICollection<OrphanSummary> orphanSummaries, OrphanSummary context, ref uint serviceCount, uint maxSampleSize)
        {
            // only log operation contexts up to sample size, otherwise ignore
            if (orphanSummaries.Count < maxSampleSize)
            {
                orphanSummaries.Add(context);
            }
            serviceCount += 1;
        }

        private static void AddServiceToResult(JObject array, string serviceName, ICollection<OrphanSummary> serviceSample, ref uint serviceCount)
        {
            if (serviceSample.Any())
            {
                array.Add(CreateOrphanJson(serviceName, serviceSample, serviceCount));
                serviceSample.Clear();
                serviceCount = 0;
            }
        }

        public void Dispose()
        {
            _source?.Cancel();

            if (_queue != null)
            {
                _queue.Writer.TryComplete();
            }
        }

        public void Add(OrphanSummary orphanSummary)
        {
            if (!_source.IsCancellationRequested)
            {
                // If we've been disposed this will simply return false, which we ignore
                _queue.Writer.TryWrite(orphanSummary);
            }
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
