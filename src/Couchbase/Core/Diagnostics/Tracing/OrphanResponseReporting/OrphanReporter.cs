using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
        private readonly BlockingCollection<OrphanSummary> _queue = new(1000);
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
            Task.Factory.StartNew(DoWork, TaskCreationOptions.LongRunning);
        }

        private async Task DoWork()
        {
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

                    while (!_source.IsCancellationRequested && !_queue.IsAddingCompleted && _queue.TryTake(out var context, WorkerSleep, _source.Token))
                    {
                        // protects against there being lots of orphans blocking the process from existing if cancelled
                        if (_source.IsCancellationRequested)
                        {
                            break;
                        }

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
                                AddContextToService(_searchOrphans, context, ref _searchOrphanCount, SampleSize);
                                break;
                            case OuterRequestSpans.ServiceSpan.AnalyticsQuery:
                                AddContextToService(_analyticsOrphans, context, ref _analyticsOrphanCount, SampleSize);
                                break;
                            default:
                                _logger.LogInformation($"Unknown service type {context.ServiceType} for operation with ID '{context.operation_id}'");
                                break;
                        }

                        _hasOrphans = true; // indicates we have something to process
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
                _queue.CompleteAdding();
                while (_queue.Any())
                {
                    _queue.TryTake(out _);
                }

                _queue.Dispose();
            }
        }

        public void Add(OrphanSummary orphanSummary)
        {
            if (!_source.IsCancellationRequested && !_queue.IsAddingCompleted)
            {
                _queue.Add(orphanSummary, _source.Token);
            }
        }
    }
}
