using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.DataStructures
{
    public sealed class PersistentQueue<TValue> : PersistentStoreBase<TValue>, IPersistentQueue<TValue>
    {
        private readonly QueueOptions _options;

        internal PersistentQueue(ICouchbaseCollection collection, string key, QueueOptions options, ILogger? logger, IRedactor? redactor)
            : base(collection, key, logger, redactor, new object(), false)
        {
            _options = options;
        }

        public TValue? Dequeue()
        {
            return DequeueAsync().GetAwaiter().GetResult();
        }

        public async Task<TValue?>  DequeueAsync()
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            using var cts = new CancellationTokenSource(_options.Timeout ?? ClusterOptions.Default.KvTimeout);
            int retriesUsed = 0;
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var result = await Collection.LookupInAsync(Key, builder => builder.Get("[-1]"))
                        .ConfigureAwait(false);
                    var item = result.ContentAs<TValue>(0);

                    await Collection.MutateInAsync(Key, builder => builder.Remove("[-1]"),
                        options => options.Cas(result.Cas)).ConfigureAwait(false);

                    return item;
                }
                catch (CasMismatchException cm)
                {
                    retriesUsed++;
                    if (retriesUsed > _options.CasMismatchRetries)
                    {
                        throw new CouchbaseException(
                            $"Couldn't perform dequeue in fewer than {_options.CasMismatchRetries} iterations. "
                            + "It is likely concurrent modifications of this document are the reason.", cm);
                    }

                    // add a slight fuzz factor to mitigate contention.
                    await Task.Delay(retriesUsed * 10, cts.Token).ConfigureAwait(false);
                }
            }

            throw new UnambiguousTimeoutException("Unable to dequeue in the given time");
        }

        [Obsolete("This method is blocking; please use the async version instead.")]
        public void Enqueue(TValue item)
        {
            EnqueueAsync(item).GetAwaiter().GetResult();
        }

        public async Task EnqueueAsync(TValue item)
        {
            await CreateBackingStoreAsync().ConfigureAwait(false);
            await Collection.MutateInAsync(Key, builder => builder.ArrayAppend("", item)).ConfigureAwait(false);
        }

        [Obsolete("This method is blocking; please use the async version instead.")]
        public TValue? Peek()
        {
            return PeekAsync().GetAwaiter().GetResult();
        }

        public async Task<TValue?> PeekAsync()
        {
            var result = await Collection.LookupInAsync(Key, builder => builder.Get("[0]")).ConfigureAwait(false);
            return result.ContentAs<TValue>(0);
        }
    }

    /// <summary>
    /// Behavior options for the <see cref="IPersistentQueue{T}"/> implementation.
    /// </summary>
    /// <param name="CasMismatchRetries">For operations that use Cas, such as <see cref="IPersistentQueue{T}.Dequeue"/>
    /// the number of times a CasMismatchException will be retried internally.</param>
    /// <param name="Timeout">The timeout value for operations that involve retries.  Defaults to KvTimeout.</param>
    public sealed record QueueOptions(int CasMismatchRetries = QueueOptions.DefaultCasMismatchRetries, TimeSpan? Timeout = null)
    {
        public const int DefaultCasMismatchRetries = 10;
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
