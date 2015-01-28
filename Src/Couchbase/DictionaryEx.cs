using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase
{
    public static class DictionaryEx
    {
        internal static bool TryGetValue<TValue>(this IDictionary<string, object> dict, string key, out TValue result)
        {
            object tmp;

            if (dict.TryGetValue(key, out tmp))
            {
                result = (TValue)tmp;

                return true;
            }

            result = default(TValue);

            return false;
        }

        public static IDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return new ReadOnlyDictionary<TKey, TValue>(dictionary);
        }

        #region [ ReadOnlyDictionary           ]

        private class ReadOnlyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
        {
            private IDictionary<TKey, TValue> original;

            public ReadOnlyDictionary(IDictionary<TKey, TValue> original)
            {
                if (original == null)
                    throw new ArgumentNullException("original");

                this.original = original;
            }

            private static Exception ItsReadOnly()
            {
                return new NotSupportedException("Dictionary is read-only!");
            }

            void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
            {
                throw ItsReadOnly();
            }

            bool IDictionary<TKey, TValue>.ContainsKey(TKey key)
            {
                return this.original.ContainsKey(key);
            }

            ICollection<TKey> IDictionary<TKey, TValue>.Keys
            {
                get { return this.original.Keys; }
            }

            bool IDictionary<TKey, TValue>.Remove(TKey key)
            {
                throw ItsReadOnly();
            }

            bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
            {
                return this.original.TryGetValue(key, out value);
            }

            ICollection<TValue> IDictionary<TKey, TValue>.Values
            {
                get { return this.original.Values; }
            }

            TValue IDictionary<TKey, TValue>.this[TKey key]
            {
                get { return this.original[key]; }
                set { throw ItsReadOnly(); }
            }

            void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
            {
                throw ItsReadOnly();
            }

            void ICollection<KeyValuePair<TKey, TValue>>.Clear()
            {
                throw ItsReadOnly();
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
            {
                return this.original.Contains(item);
            }

            void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                this.original.CopyTo(array, arrayIndex);
            }

            int ICollection<KeyValuePair<TKey, TValue>>.Count
            {
                get { return this.original.Count; }
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly
            {
                get { return true; }
            }

            bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
            {
                throw ItsReadOnly();
            }

            IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
            {
                return this.original.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.original.GetEnumerator();
            }
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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