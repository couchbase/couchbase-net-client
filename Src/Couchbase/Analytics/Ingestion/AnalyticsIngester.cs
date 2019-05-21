using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Utils;

namespace Couchbase.Analytics.Ingestion
{
    public static class AnalyticsExtensions
    {
        /// <summary>
        /// Executes a query and ingests the results as documents into Couchbase server for further analytics.
        /// <para>NOTE: This is an experimental feature and is subject to change.</para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="bucket"></param>
        /// <param name="request"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static async Task<IList<IOperationResult<T>>> IngestAsync<T>(this IBucket bucket, IAnalyticsRequest request, IngestOptions options = null)
        {
            //use defaults if not options not explicitly passed
            options = options ?? new IngestOptions();

            if (options.CancellationTokenValue.IsCancellationRequested)
            {
                options.CancellationTokenValue.ThrowIfCancellationRequested();
            }

            //execute the analytics query
            var analyticsResult =
                await bucket.QueryAsync<T>(request, options.CancellationTokenValue).ContinueOnAnyContext();

            //the initial analytics query has failed so immediately abort
            if (!analyticsResult.Success)
            {
                throw new AnalyticsException(analyticsResult.Message, analyticsResult.Exception)
                {
                    Errors = analyticsResult.Errors
                };
            }

            //use the IngestMethod
            var results = new ConcurrentBag<Task<IOperationResult<T>>>();
            foreach (var row in analyticsResult.Rows)
            {
                Task<IOperationResult<T>> op;
                switch (options.IngestMethodValue)
                {
                    case IngestMethod.Insert:
                        op = bucket.InsertAsync(options.IdGeneratorValue(row), row, options.ExpirationValue, options.TimeoutValue);
                        break;
                    case IngestMethod.Upsert:
                        op = bucket.UpsertAsync(options.IdGeneratorValue(row), row, options.ExpirationValue, options.TimeoutValue);
                        break;
                    case IngestMethod.Replace:
                        op = bucket.ReplaceAsync(options.IdGeneratorValue(row), row, options.ExpirationValue, options.TimeoutValue);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                results.Add(op);
            }

            return await Task.WhenAll(results).ContinueOnAnyContext();
        }
    }
}
