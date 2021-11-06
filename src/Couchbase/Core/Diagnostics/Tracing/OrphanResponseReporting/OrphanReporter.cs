using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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

        // Queue to hold items until they can be processed into the summary collections and counts.
        // We do this to avoid blocking operation processing any longer than necessary.
        private readonly ConcurrentQueue<OrphanSummary> _queue = new();

        // Lock to control access to the summary collections and counts
        private readonly object _lock = new();

        private readonly List<OrphanSummary> _kvOrphans = new();
        private readonly List<OrphanSummary> _viewOrphans = new();
        private readonly List<OrphanSummary> _queryOrphans = new();
        private readonly List<OrphanSummary> _searchOrphans = new();
        private readonly List<OrphanSummary> _analyticsOrphans = new();

        public int Interval { get; set; }
        public uint SampleSize { get; set; }

        private uint _kvOrphanCount;
        private uint _viewOrphanCount;
        private uint _queryOrphanCount;
        private uint _searchOrphanCount;
        private uint _analyticsOrphanCount;
        private volatile bool _hasOrphans;

        /// <summary>
        /// Internal total count of all pending operation contexts to have been recorded.
        /// </summary>
        internal uint TotalCount => _kvOrphanCount + _viewOrphanCount + _queryOrphanCount + _searchOrphanCount + _analyticsOrphanCount;

        public OrphanReporter(ILogger<OrphanReporter> logger, OrphanOptions options)
        {
            _logger = logger;
            Interval = (int)options.EmitInterval.TotalMilliseconds;
            SampleSize = options.SampleSize;

            // Ensure that we don't flow the ExecutionContext into the long running tasks
            using (ExecutionContext.SuppressFlow())
            {
                Task.Run(ProcessSummary);
                Task.Run(ProcessQueue);
            }
        }

        private async Task ProcessSummary()
        {
            while (!_source.IsCancellationRequested)
            {
                try
                {
                    // This routine is the only one that transitions _hasOrphans from true to false,
                    // so we don't need to recheck this inside the lock
                    if (_hasOrphans)
                    {
                        lock (_lock)
                        {
                            var result = new JObject();
                            AddServiceToResult(
                                result, OuterRequestSpans.ServiceSpan.Kv.Name, _kvOrphans, ref _kvOrphanCount);
                            AddServiceToResult(
                                result, OuterRequestSpans.ServiceSpan.ViewQuery, _viewOrphans, ref _viewOrphanCount);
                            AddServiceToResult(
                                result, OuterRequestSpans.ServiceSpan.N1QLQuery, _queryOrphans, ref _queryOrphanCount);
                            AddServiceToResult(
                                result, OuterRequestSpans.ServiceSpan.SearchQuery, _searchOrphans,
                                ref _searchOrphanCount);
                            AddServiceToResult(
                                result, OuterRequestSpans.ServiceSpan.AnalyticsQuery, _analyticsOrphans,
                                ref _analyticsOrphanCount);

                            if (result.HasValues)
                            {
                                _logger.LogWarning("Orphaned responses observed: {0}",
                                    result.ToString(Formatting.None));
                            }

                            _hasOrphans = false;
                        }
                    }

                    await Task.Delay(Interval, _source.Token).ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { } // ignore
                catch (OperationCanceledException) { } // ignore
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error when processing Orphaned Response summary");
                }
            }
        }

        private async Task ProcessQueue()
        {
            while (!_source.IsCancellationRequested)
            {
                try
                {
                    // Repeat until _source is cancelled or we have no more data AND have waited at least WorkerSleep
                    while (!_source.IsCancellationRequested && _queue.TryDequeue(out var context))
                    {
                        // Here we take out the lock inside the loop. This avoids locking when the queue is empty,
                        // which is by far the most common. It also ensures that, in the case of a flood, we don't
                        // hold the lock too long and prevent the summary from being output.
                        lock (_lock)
                        {
                            switch (context.ServiceType)
                            {
                                case OuterRequestSpans.ServiceSpan.Kv.Name:
                                    AddContextToService(_kvOrphans, context, ref _kvOrphanCount, SampleSize);
                                    break;
                                case OuterRequestSpans.ServiceSpan.ViewQuery:
                                    AddContextToService(_viewOrphans, context, ref _viewOrphanCount,
                                        SampleSize);
                                    break;
                                case OuterRequestSpans.ServiceSpan.N1QLQuery:
                                    AddContextToService(_queryOrphans, context, ref _queryOrphanCount,
                                        SampleSize);
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

                    // sleep for a little while
                    await Task.Delay(WorkerSleep, _source.Token).ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { } // ignore
                catch (OperationCanceledException) { } // ignore
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error when processing Orphaned Responses");
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
            if (serviceSample.Count > 0)
            {
                array.Add(CreateOrphanJson(serviceName, serviceSample, serviceCount));
                serviceSample.Clear();
                serviceCount = 0;
            }
        }

        public void Dispose()
        {
            _source?.Cancel();
        }

        public void Add(OrphanSummary orphanSummary)
        {
            if (!_source.IsCancellationRequested)
            {
                // If we've been disposed this will simply return false, which we ignore
                _queue.Enqueue(orphanSummary);
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
