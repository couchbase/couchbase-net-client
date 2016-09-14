using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;
using Newtonsoft.Json;

namespace Couchbase.Collections
{
    /// <summary>
    /// Represents a collection of keys and values stored in Couchbase Server.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <seealso cref="System.Collections.Generic.IDictionary{TKey, TValue}" />
    public class CouchbaseDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        /// <summary>
        /// The bucket that contains the collection document.
        /// </summary>
        protected readonly IBucket Bucket;

        /// <summary>
        /// Initializes a new instance of the <see cref="CouchbaseDictionary{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public CouchbaseDictionary(IBucket bucket, string key)
        {
            if (bucket == null || string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentNullException(bucket == null ? "bucket" : "key");
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
                var insert = Bucket.Insert(new Document<Dictionary<TKey, TValue>>
                {
                    Id = Key,
                    Content = new Dictionary<TKey, TValue>()
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
        /// Gets the dictionary document.
        /// </summary>
        /// <returns></returns>
        protected virtual IDictionary<TKey, TValue> GetDictionary()
        {
            var get = Bucket.Get<IDictionary<TKey, TValue>>(Key);
            if (!get.Success && get.Exception != null)
            {
                throw get.Exception;
            }
            return get.Value;
        }

        /// <summary>
        /// Gets an object that can be used to synchronize access to the <see cref="T:System.Collections.ICollection" />.
        /// </summary>
        public object SyncRoot { get; private set; }

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
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return GetDictionary().GetEnumerator();
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
        /// Adds an item to the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2y" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2y" />.</param>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        /// <summary>
        /// Removes all items from the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2y" />.
        /// </summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Clear()
        {
            var replace = Bucket.Replace(new Document<Dictionary<TKey, TValue>>
            {
                Id = Key,
                Content = new Dictionary<TKey, TValue>()
            });

            if (!replace.Success)
            {
                if (replace.Exception != null)
                {
                    throw replace.Exception;
                }
                throw new InvalidOperationException(replace.Status.ToString());
            }
        }

        /// <summary>
        /// Determines whether the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" /> contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> is found in the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />; otherwise, false.
        /// </returns>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            TValue found;
            if (TryGetValue(item.Key, out found))
            {
                return found.Equals(item.Value);
            }
            return false;
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="System.ArgumentNullException">array</exception>
        /// <exception cref="System.IndexOutOfRangeException"></exception>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (arrayIndex < 0) throw new IndexOutOfRangeException();

            // ReSharper disable once InconsistentlySynchronizedField
            var items = GetDictionary();

            for (var i = arrayIndex; i < items.Count; i++)
            {
                array[i] = items.ElementAt(i);
            };
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </summary>
        /// <param name="item">The object to remove from the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </returns>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </summary>
        public int Count
        {
            get
            {
                var items = GetDictionary();
                return items.Count;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" /> is read-only.
        /// </summary>
        public bool IsReadOnly { get { return false; } }

        /// <summary>
        /// Determines whether the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" /> contains an element with the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.</param>
        /// <returns>
        /// true if the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" /> contains an element with the key; otherwise, false.
        /// </returns>
        public bool ContainsKey(TKey key)
        {
            var exists = Bucket.LookupIn<IDictionary<TKey, TValue>>(Key).
                Exists(key.ToString()).
                Execute();

            if (!exists.Success)
            {
                if (exists.Exception != null)
                {
                    throw exists.Exception;
                }
            }
            return exists.Success;
        }

        /// <summary>
        /// Adds an element with the provided key and value to the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </summary>
        /// <param name="key">The object to use as the key of the element to add.</param>
        /// <param name="value">The object to use as the value of the element to add.</param>
        /// <exception cref="System.ArgumentNullException">key</exception>
        /// <exception cref="System.ArgumentException">Key exists.</exception>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Add(TKey key, TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            var sKey = key.ToString();
            var lookup = Bucket.LookupIn<IDictionary<TKey, TValue>>(Key).
                Exists(sKey).
                Execute();

            if (lookup.Success)
            {
                throw new ArgumentException("Key exists.");
            }

            var insert = Bucket.MutateIn<IDictionary<TKey, TValue>>(Key).
                Insert(sKey, value).
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

        protected void Upsert(TKey key, TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            var sKey = key.ToString();
            var upsert = Bucket.MutateIn<IDictionary<TKey, TValue>>(Key).
                Upsert(sKey, value).
                Execute();

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
        /// Removes the element with the specified key from the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>
        /// true if the element is successfully removed; otherwise, false.  This method also returns false if <paramref name="key" /> was not found in the original <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">key</exception>
        public bool Remove(TKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            var remove = Bucket.MutateIn<Dictionary<TKey, TValue>>(Key).
                Remove(key.ToString()).
                Execute();

            if (!remove.Success)
            {
                if (remove.Exception != null)
                {
                    throw remove.Exception;
                }
            }
            return remove.Success;
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">When this method returns, the value associated with the specified key, if the key is found; otherwise, the default value for the type of the <paramref name="value" /> parameter. This parameter is passed uninitialized.</param>
        /// <returns>
        /// true if the object that implements <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" /> contains an element with the specified key; otherwise, false.
        /// </returns>
        /// <exception cref="System.ArgumentNullException">key</exception>
        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            var get = Bucket.LookupIn<Dictionary<TKey, TValue>>(Key).
                Get(key.ToString()).
                Execute();

            if (!get.Success)
            {
                if (get.Exception != null)
                {
                    throw get.Exception;
                }

                value = default(TValue);
                return get.Success;
            }

            value = get.Content<TValue>(0);
            return get.Success;
        }

        /// <summary>
        /// Gets or sets the <see cref="TValue"/> with the specified key.
        /// </summary>
        /// <value>
        /// The <see cref="TValue"/>.
        /// </value>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException"></exception>
        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (TryGetValue(key, out value))
                {
                    return value;
                }
                throw new KeyNotFoundException(key.ToString());
            }
            set { Upsert(key, value); }
        }

        /// <summary>
        /// Gets an <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" /> containing the keys of the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </summary>
        public ICollection<TKey> Keys
        {
            get
            {
                var items = GetDictionary();
                return items.Keys;
            }
        }

        /// <summary>
        /// Gets an <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" /> containing the values in the <see cref="T:Couchbase.Collections.CouchbaseDictionary`2" />.
        /// </summary>
        public ICollection<TValue> Values
        {
            get
            {
                var items = GetDictionary();
                return items.Values;
            }
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
