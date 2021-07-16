using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.Analytics
{
    /// <summary>
    /// Analytics related extensions for <see cref="ICluster"/>.
    /// </summary>
    public static class AnalyticsExtensions
    {
        /// <summary>
        /// Executes a query and ingests the results as documents into Couchbase server for further analytics.
        /// <para>
        /// NOTE: This is an experimental API and may change in the future.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cluster"></param>
        /// <param name="collection"></param>
        /// <param name="statement"></param>
        /// <param name="configureOptions"></param>
        /// <returns></returns>
        public static Task<IEnumerable<IMutationResult>> IngestAsync<T>(this ICluster cluster, string statement, ICouchbaseCollection collection, Action<IngestOptions> configureOptions)
        {
            var options = new IngestOptions();
            configureOptions(options);

            return IngestAsync<T>(
                cluster,
                statement,
                collection,
                options
            );
        }

        /// <summary>
        /// Executes a query and ingests the results as documents into Couchbase server for further analytics.
        /// <para>
        /// NOTE: This is an experimental API and may change in the future.
        /// </para>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cluster"></param>
        /// <param name="collection"></param>
        /// <param name="statement"></param>
        /// <param name="ingestOptions"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<IMutationResult>> IngestAsync<T>(this ICluster cluster, string statement, ICouchbaseCollection collection, IngestOptions ingestOptions = null)
        {
            //use defaults if not options not explicitly passed
            ingestOptions ??= new IngestOptions();

            if (ingestOptions.TokenValue.IsCancellationRequested)
            {
                ingestOptions.TokenValue.ThrowIfCancellationRequested();
            }

            //execute the analytics query
            var result = await cluster.AnalyticsQueryAsync<T>(
                statement,
                options => options.CancellationToken(ingestOptions.TokenValue)
            ).ConfigureAwait(false);

            // ingest result into collection
            var results = new ConcurrentBag<Task<IMutationResult>>();
            await foreach (var row in result.WithCancellation(ingestOptions.TokenValue).ConfigureAwait(false))
            {
                Task<IMutationResult> op;
                switch (ingestOptions.IngestMethodValue)
                {
                    case IngestMethod.Insert:
                        op = collection.InsertAsync(
                            ingestOptions.IdGeneratorValue(row),
                            row,
                            options =>
                            {
                                options.Expiry(ingestOptions.ExpiryValue);
                                options.Timeout(ingestOptions.TimeoutValue);
                            });
                        break;
                    case IngestMethod.Upsert:
                        op = collection.UpsertAsync(
                            ingestOptions.IdGeneratorValue(row),
                            row,
                            options =>
                            {
                                options.Expiry(ingestOptions.ExpiryValue);
                                options.Timeout(ingestOptions.TimeoutValue);
                            });
                        break;
                    case IngestMethod.Replace:
                        op = collection.ReplaceAsync(
                            ingestOptions.IdGeneratorValue(row),
                            row,
                            options =>
                            {
                                options.Expiry(ingestOptions.ExpiryValue);
                                options.Timeout(ingestOptions.TimeoutValue);
                            });
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                results.Add(op);
            }

            return await Task.WhenAll(results).ConfigureAwait(false);
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
