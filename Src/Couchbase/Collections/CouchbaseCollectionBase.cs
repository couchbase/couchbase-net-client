using System;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Core;

namespace Couchbase.Collections
{
    public abstract class CouchbaseCollectionBase<T> : ICollection
    {
        protected readonly IBucket Bucket;

        /// <summary>
        /// Initializes a new instance of the <see cref="CouchbaseList{T}"/> class.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">bucket</exception>
        protected CouchbaseCollectionBase(IBucket bucket, string key)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException("bucket");
            }
            Bucket = bucket;
            Key = key;
            CreateBackingStore();
            SyncRoot = new object();
        }

        /// <summary>
        /// Creates the backing store.
        /// </summary>
        protected void CreateBackingStore()
        {
            if (!Bucket.Exists(Key))
            {
                var insert = Bucket.Insert(new Document<List<T>>
                {
                    Id = Key,
                    Content = new List<T>()
                });

                if (!insert.Success)
                {
                    if (insert.Exception != null)
                    {
                        throw insert.Exception;
                    }
                    throw new InvalidOperationException(insert.Status.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the key for this list.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        public string Key { get; private set; }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// An enumerator that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            var get = Bucket.Get<List<T>>(Key);

            if (!get.Success)
            {
                if (get.Exception != null)
                {
                    throw get.Exception;
                }
                throw new InvalidOperationException(get.Status.ToString());
            }

            return get.Value.GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        public void Clear()
        {
            var upsert = Bucket.Upsert(new Document<List<T>>
            {
                Id = Key,
                Content = new List<T>()
            });
            if (!upsert.Success)
            {
                if (upsert.Exception != null)
                {
                    throw upsert.Exception;
                }
                throw new InvalidOperationException(upsert.Status.ToString());
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="System.ArgumentNullException">array</exception>
        /// <exception cref="System.IndexOutOfRangeException"></exception>
        /// <exception cref="System.ArgumentException">array is not large enough.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (arrayIndex < 0) throw new IndexOutOfRangeException();

            // ReSharper disable once InconsistentlySynchronizedField
            var get = Bucket.Get<List<T>>(Key);

            if (!get.Success)
            {
                if (get.Exception != null)
                {
                    throw get.Exception;
                }
                throw new InvalidOperationException(get.Status.ToString());
            }

            var items = get.Value;
            for (var i = arrayIndex; i < items.Count; i++)
            {
                array[i] = items[i];
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.ICollection" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.ICollection" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        public void CopyTo(Array array, int index)
        {
            CopyTo((T[])array, index);
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        public int Count
        {
            get
            {
                var get = Bucket.Get<List<T>>(Key);

                if (!get.Success)
                {
                    if (get.Exception != null)
                    {
                        throw get.Exception;
                    }
                    throw new InvalidOperationException(get.Status.ToString());
                }
                return get.Value.Count;
            }
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.
        /// </summary>
        public object SyncRoot { get; private set; }

        /// <summary>
        /// Gets a value indicating whether access to the <see cref="T:System.Collections.ICollection" /> is synchronized (thread safe).
        /// </summary>
        public bool IsSynchronized { get; private set; }
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
