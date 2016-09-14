using System;
using System.Collections.Generic;
using Couchbase.Core;

namespace Couchbase.Collections
{
    /// <summary>
    /// Represents a collection of objects, stored in Couchbase server, that can be individually accessed by index.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <seealso cref="System.Collections.Generic.IList{T}" />
    public class CouchbaseList<T> : CouchbaseCollectionBase<T>, IList<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly object SyncObj = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="CouchbaseList{T}"/> class.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">bucket</exception>
        public CouchbaseList(IBucket bucket, string key) : base(bucket, key)
        {
        }

        /// <summary>
        /// Adds an item to the <see cref="T:Couchbase.Collections.CouchbaseList`1" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:Couchbase.Collections.CouchbaseList`1" />.</param>
        public void Add(T item)
        {
            var append = Bucket.MutateIn<List<T>>(Key).
                ArrayAppend(item, true).
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
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public bool Contains(T item)
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

            var items = get.Value;

            return items.Contains(item);
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public bool Remove(T item)
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

            var items = get.Value;

            return items.Remove(item);
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </summary>
        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1" />.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        /// <returns>
        /// The index of <paramref name="item" /> if found in the list; otherwise, -1.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public int IndexOf(T item)
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

            var items = get.Value;

            return items.IndexOf(item);
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1" /> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
        /// <param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1" />.</param>
        public void Insert(int index, T item)
        {
            if (index < 0) throw new IndexOutOfRangeException();
            if (index > Count) throw new IndexOutOfRangeException();

            lock (SyncObj)
            {
                var insert = Bucket.MutateIn<List<T>>(Key).
                    ArrayInsert("[" + index + "]", item, true).
                    Execute();

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
        /// Removes the <see cref="T:System.Collections.Generic.IList`1" /> item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="System.IndexOutOfRangeException"></exception>
        public void RemoveAt(int index)
        {
            if(index < 0) throw new IndexOutOfRangeException();
            if (index > Count) throw new IndexOutOfRangeException();

            lock (SyncObj)
            {
                var remove = Bucket.MutateIn<List<T>>(Key).
                    Remove("[" + index + "]").
                    Execute();

                if (!remove.Success)
                {
                    if (remove.Exception != null)
                    {
                        throw remove.Exception;
                    }
                    throw new InvalidOperationException(remove.Status.ToString());
                }
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="T"/> at the specified index.
        /// </summary>
        /// <value>
        /// The <see cref="T"/>.
        /// </value>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        public T this[int index]
        {
            get { return Get(index); }
            set { Insert(index, value); }
        }

        /// <summary>
        /// Gets the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns></returns>
        private T Get(int index)
        {
            if (index < 0) throw new IndexOutOfRangeException();
            if (index > Count) throw new IndexOutOfRangeException();

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

            return items[index];
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
