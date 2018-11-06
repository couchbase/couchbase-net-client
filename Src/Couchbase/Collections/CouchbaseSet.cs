using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core;

namespace Couchbase.Collections
{
    /// <summary>
    /// Provides a Couchbase persisted set.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <seealso cref="System.Collections.Generic.ISet{TValue}" />
    public class CouchbaseSet<TValue> : ISet<TValue>
    {
        protected readonly IBucket Bucket;

        /// <summary>
        /// Initializes a new instance of the <see cref="CouchbaseList{T}"/> class.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <param name="key">The key.</param>
        /// <exception cref="System.ArgumentNullException">bucket</exception>
        public CouchbaseSet(IBucket bucket, string key)
        {
            if (bucket == null)
            {
                throw new ArgumentNullException("bucket");
            }
            Bucket = bucket;
            Key = key;
            CreateBackingStore();
        }

        /// <summary>
        /// Creates the backing store.
        /// </summary>
        protected void CreateBackingStore()
        {
            if (!Bucket.Exists(Key))
            {
                var insert = Bucket.Insert(new Document<HashSet<TValue>>
                {
                    Id = Key,
                    Content = new HashSet<TValue>()
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
        /// Gets the set.
        /// </summary>
        /// <returns></returns>
        protected HashSet<TValue> GetSet()
        {
            var get = Bucket.Get<HashSet<TValue>>(Key);
            if (!get.Success)
            {
                if (get.Exception != null)
                {
                    throw get.Exception;
                }
                throw new InvalidOperationException(get.Status.ToString());
            }
            return get.Value;
        }

        /// <summary>
        /// Upserts the specified the set.
        /// </summary>
        /// <param name="theSet">The set.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        protected void Upsert(IEnumerable<TValue> theSet)
        {
            var upsert = Bucket.Replace(new Document<IEnumerable<TValue>>
            {
                Id = Key,
                Content = theSet,
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
        public IEnumerator<TValue> GetEnumerator()
        {
            return GetSet().GetEnumerator();
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
        /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Add(TValue item)
        {
            var thisSet = GetSet();
            if (thisSet.Add(item))
            {
                var add = Bucket.Replace(new Document<HashSet<TValue>>
                {
                    Id = Key,
                    Content = thisSet
                });
                if (!add.Success)
                {
                    if (add.Exception != null)
                    {
                        throw add.Exception;
                    }

                    throw new InvalidOperationException(add.Status.ToString());
                }
            }
            else
            {
                throw new InvalidOperationException("Item exists.");
            }
        }

        /// <summary>
        /// Modifies the current set so that it contains all elements that are present in the current set, in the specified collection, or in both.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        public void UnionWith(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            var union = thisSet.Union(other);
            Upsert(union);
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are also in a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        public void IntersectWith(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            var intersect = thisSet.Intersect(other);
            Upsert(intersect);
        }

        /// <summary>
        /// Removes all elements in the specified collection from the current set.
        /// </summary>
        /// <param name="other">The collection of items to remove from the set.</param>
        public void ExceptWith(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            thisSet.ExceptWith(other);
            Upsert(thisSet);
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        public void SymmetricExceptWith(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            thisSet.SymmetricExceptWith(other);
            Upsert(thisSet);
        }

        /// <summary>
        /// Determines whether a set is a subset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// true if the current set is a subset of <paramref name="other" />; otherwise, false.
        /// </returns>
        public bool IsSubsetOf(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            return thisSet.IsSubsetOf(other);
        }

        /// <summary>
        /// Determines whether the current set is a superset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// true if the current set is a superset of <paramref name="other" />; otherwise, false.
        /// </returns>
        public bool IsSupersetOf(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            return thisSet.IsSupersetOf(other);
        }

        /// <summary>
        /// Determines whether the current set is a proper (strict) superset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// true if the current set is a proper superset of <paramref name="other" />; otherwise, false.
        /// </returns>
        public bool IsProperSupersetOf(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            return thisSet.IsProperSupersetOf(other);
        }

        /// <summary>
        /// Determines whether the current set is a proper (strict) subset of a specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// true if the current set is a proper subset of <paramref name="other" />; otherwise, false.
        /// </returns>
        public bool IsProperSubsetOf(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            return thisSet.IsProperSubsetOf(other);
        }

        /// <summary>
        /// Determines whether the current set overlaps with the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// true if the current set and <paramref name="other" /> share at least one common element; otherwise, false.
        /// </returns>
        public bool Overlaps(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            return thisSet.Overlaps(other);
        }

        /// <summary>
        /// Determines whether the current set and the specified collection contain the same elements.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// true if the current set is equal to <paramref name="other" />; otherwise, false.
        /// </returns>
        public bool SetEquals(IEnumerable<TValue> other)
        {
            var thisSet = GetSet();
            return thisSet.SetEquals(other);
        }

        /// <summary>
        /// Adds an element to the current set and returns a value to indicate if the element was successfully added.
        /// </summary>
        /// <param name="item">The element to add to the set.</param>
        /// <returns>
        /// true if the element is added to the set; false if the element is already in the set.
        /// </returns>
        bool ISet<TValue>.Add(TValue item)
        {
            try
            {
                Add(item);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void Clear()
        {
            var upsert = Bucket.Upsert(new Document<HashSet<TValue>>
            {
                Id = Key,
                Content = new HashSet<TValue>()
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
        /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false.
        /// </returns>
        public bool Contains(TValue item)
        {
            try
            {
                var thisSet = GetSet();
                return thisSet.Contains(item);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
        /// <exception cref="System.ArgumentNullException">array</exception>
        /// <exception cref="System.IndexOutOfRangeException"></exception>
        /// <exception cref="System.InvalidOperationException"></exception>
        public void CopyTo(TValue[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException("array");
            if (arrayIndex < 0) throw new IndexOutOfRangeException();

            // ReSharper disable once InconsistentlySynchronizedField
            var get = Bucket.Get<HashSet<TValue>>(Key);

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
                array[i] = items.ElementAt(i);
            }
        }

        /// <summary>
        /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
        /// <returns>
        /// true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </returns>
        public bool Remove(TValue item)
        {
            try
            {
                var thisSet = GetSet();
                thisSet.Remove(item);
                Upsert(thisSet);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
        /// </summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        public int Count
        {
            get
            {
                var get = Bucket.Get<HashSet<TValue>>(Key);

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
        /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
        /// </summary>
        public bool IsReadOnly { get { return false; } }
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
