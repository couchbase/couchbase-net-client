using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal class OrphanedResponseLogger : IOrphanedResponseLogger
    {
        private const int WorkerSleep = 100;
        private readonly ILogger<OrphanedResponseLogger> _logger;

        private readonly CancellationTokenSource _source = new CancellationTokenSource();
        private readonly BlockingCollection<OperationContext> _queue = new BlockingCollection<OperationContext>(1000);
        private readonly List<OperationContext> _kvOrphans = new List<OperationContext>();
        private readonly List<OperationContext> _viewOrphans = new List<OperationContext>();
        private readonly List<OperationContext> _queryOrphans = new List<OperationContext>();
        private readonly List<OperationContext> _searchOrphans = new List<OperationContext>();
        private readonly List<OperationContext> _analyticsOrphans = new List<OperationContext>();

        public int Interval { get; set; } = 10000; // 10 seconds
        public int SampleSize { get; set; } = 10;

        private DateTime _lastrun = DateTime.UtcNow;
        private int _kvOrphanCount;
        private int _viewOrphanCount;
        private int _queryOrphanCount;
        private int _searchOrphanCount;
        private int _analyticsOrphanCount;
        private bool _hasOrphans;

        /// <summary>
        /// Internal total count of all pending operation contexts to have been recorded.
        /// </summary>
        internal int TotalCount => _kvOrphanCount + _viewOrphanCount + _queryOrphanCount + _searchOrphanCount + _analyticsOrphanCount;

        public OrphanedResponseLogger(ILogger<OrphanedResponseLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Task.Factory.StartNew(DoWork, TaskCreationOptions.LongRunning);
        }

        private async Task DoWork()
        {
            while (!_source.IsCancellationRequested)
            {
                try
                {
                    // determine if we need to write to log yet
                    if (DateTime.UtcNow.Subtract(_lastrun) > TimeSpan.FromMilliseconds(Interval))
                    {
                        if (_hasOrphans)
                        {
                            var result = new JArray();
                            AddServiceToResult(result, CouchbaseTags.ServiceKv, _kvOrphans, ref _kvOrphanCount);
                            AddServiceToResult(result, CouchbaseTags.ServiceView, _viewOrphans, ref _viewOrphanCount);
                            AddServiceToResult(result, CouchbaseTags.ServiceQuery, _queryOrphans, ref _queryOrphanCount);
                            AddServiceToResult(result, CouchbaseTags.ServiceSearch, _searchOrphans, ref _searchOrphanCount);
                            AddServiceToResult(result, CouchbaseTags.ServiceAnalytics, _analyticsOrphans, ref _analyticsOrphanCount);

                            if (result.Any())
                            {
                                _logger.LogWarning("Orphaned responses observed: {0}", result.ToString(Formatting.None));
                            }

                            _hasOrphans = false;
                        }

                        _lastrun = DateTime.UtcNow;
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
                            case CouchbaseTags.ServiceKv:
                                AddContextToService(_kvOrphans, context, ref _kvOrphanCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceView:
                                AddContextToService(_viewOrphans, context, ref _viewOrphanCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceQuery:
                                AddContextToService(_queryOrphans, context, ref _queryOrphanCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceSearch:
                                AddContextToService(_searchOrphans, context, ref _searchOrphanCount, SampleSize);
                                break;
                            case CouchbaseTags.ServiceAnalytics:
                                AddContextToService(_analyticsOrphans, context, ref _analyticsOrphanCount, SampleSize);
                                break;
                            default:
                              //  Log.Info($"Unknown service type {context.ServiceType} for operation with ID '{context.OperationId}'");
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
                    _logger.LogError(exception, "Error when processing Orphaned Responses");
                }
            }
        }

        private static JObject CreateOrphanJson(string serviceType, ICollection<OperationContext> set, int totalCount)
        {
            return new JObject
            {
                {"service", serviceType},
                {"count", totalCount},
                {"top", JArray.FromObject(set)}
            };
        }

        private static void AddContextToService(ICollection<OperationContext> contexts, OperationContext context, ref int serviceCount, int maxSampleSize)
        {
            // only log operation contexts upto sample size, otherwise ignore
            if (contexts.Count < maxSampleSize)
            {
                contexts.Add(context);
            }
            serviceCount += 1;
        }

        private static void AddServiceToResult(JArray array, string serviceName, ICollection<OperationContext> serviceSample, ref int serviceCount)
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

        public void Add(OperationContext context)
        {
            if (!_source.IsCancellationRequested && !_queue.IsAddingCompleted)
            {
                _queue.Add(context, _source.Token);
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
