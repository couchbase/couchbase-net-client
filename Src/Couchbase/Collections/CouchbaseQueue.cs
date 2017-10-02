using System;
using System.Collections.Generic;
using Couchbase.Core;
using Couchbase.IO;

namespace Couchbase.Collections
{
    /// <summary>
    /// Provides a persistent Couchbase data structure with FIFO behavior.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <seealso cref="CouchbaseCollectionBase{T}" />
    public class CouchbaseQueue<T> : CouchbaseCollectionBase<T>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CouchbaseList{T}"/> class.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">bucket</exception>
        public CouchbaseQueue(IBucket bucket, string key) : base(bucket, key)
        {
        }

        /// <summary>
        /// Removes and returns the object at the beginning of the <see cref="CouchbaseQueue{T}"/>.
        /// </summary>
        /// <returns>The object that is removed from the beginning of the <see cref="CouchbaseQueue{T}"/>.</returns>
        /// <exception cref="System.InvalidOperationException">The <see cref="CouchbaseQueue{T}"/> is empty.</exception>
        public T Dequeue()
        {
            T item;
            IDocumentResult upsert;
            do
            {
                var get = Bucket.Get<List<T>>(Key);
                if (!get.Success && get.Exception != null)
                {
                    throw get.Exception;
                }

                var items = get.Value;
                if (items.Count < 1)
                {
                    throw new InvalidOperationException("The Queue<T> is empty.");
                }

                const int index = 0;
                item = items[index];
                items.RemoveAt(index);

                upsert = Bucket.Upsert(new Document<List<T>>
                {
                    Id = Key,
                    Content = items,
                    Cas = get.Cas
                });

                if (upsert.Success)
                {
                    break;
                }
            } while (upsert.Status == ResponseStatus.KeyExists);
            return item;
        }

        /// <summary>
        /// Adds an object to the end of the <see cref="CouchbaseQueue{T}"/>.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="CouchbaseQueue{T}"/>.
        /// </param>
        public void Enqueue(T item)
        {
            var append = Bucket.MutateIn<List<T>>(Key).
                ArrayAppend(item).
                Execute();

            if (!append.Success)
            {
                if (append.Exception != null)
                {
                    throw append.Exception;
                }
                throw new InvalidOperationException(append.Status.ToString());
            }
        }

        /// <summary>
        /// Returns the object at the beginning of the <see cref="CouchbaseList{T}"/> without removing it.
        /// </summary>
        /// <returns></returns>
        public T Peek()
        {
            var get = Bucket.LookupIn<List<T>>(Key).Get("[0]").
             Execute();

            if (!get.Success)
            {
                if (get.Exception != null)
                {
                    throw get.Exception;
                }
                throw new InvalidOperationException("The Queue<T> is empty.");
            }

            return get.Content<T>(0);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
