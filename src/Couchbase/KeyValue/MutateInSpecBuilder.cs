using System;
using System.Collections.Generic;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// A builder for generating an array of mutation Sub-Document operations.
    /// </summary>
    public class MutateInSpecBuilder
    {
        internal readonly List<MutateInSpec> Specs = new List<MutateInSpec>();

        /// <summary>
        /// Inserts an element into a document, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="value">The value of type "T".</param>
        /// <param name="createPath">True to create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder Insert<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Insert(path, value, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Inserts an element into a document, overriding the value if it exists
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="value">The value of type "T".</param>
        /// <param name="createPath">True to create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder Upsert<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Upsert(path, value, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Replaces an element  in a document, failing if it does not exist.
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="value">The value of type "T".</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder Replace<T>(string path, T value, bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Replace(path, value, isXattr));
            return this;
        }

        public MutateInSpecBuilder SetDoc<T>(T value)
        {
            Specs.Add(MutateInSpec.SetDoc(value));
            return this;
        }

        /// <summary>
        ///  Removes an element in a document.
        /// </summary>
        /// /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder Remove(string path, bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Remove(path, isXattr));
            return this;
        }


        /// <summary>
        ///  Inserts multiple values to the end of an array element in a document.
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="values">The values to insert.</param>
        /// <param name="createPath">True to create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder ArrayAppend<T>(string path, T[] values, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAppend(path, values, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Inserts an item to the end of an array element in a document.
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="value">The value to insert.</param>
        /// <param name="createPath">True to create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder ArrayAppend<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAppend(path, value, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Inserts multiple values to the beginning of an array element in a document.
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="values">The values to insert.</param>
        /// <param name="createParents"> Maps to 0x01 if true, otherwise omitted - create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder ArrayPrepend<T>(string path, T[] values, bool createParents = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayPrepend(path, values, createParents, isXattr));
            return this;
        }

        /// <summary>
        /// Inserts an item to the beginning of an array element in a document.
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="value">The value to insert.</param>
        /// <param name="createParents"> Maps to 0x01 if true, otherwise omitted - create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder ArrayPrepend<T>(string path, T value, bool createParents = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayPrepend(path, value, createParents, isXattr));
            return this;
        }

        /// <summary>
        /// Inserts multiple values to an array element in a document given an index
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="values">The values to insert.</param>
        /// <param name="createParents"> Maps to 0x01 if true, otherwise omitted - create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder ArrayInsert<T>(string path, T[] values, bool createParents= default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayInsert(path, values, createParents, isXattr));
            return this;
        }

        /// <summary>
        /// Inserts an item to an array element in a document given an index
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="value">The value to insert.</param>
        /// <param name="createParents"> Maps to 0x01 if true, otherwise omitted - create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder ArrayInsert<T>(string path, T value, bool createParents= default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayInsert(path, value, createParents, isXattr));
            return this;
        }

        /// <summary>
        /// Adds a value into an array element if the value does not already exist.
        /// </summary>
        /// <typeparam name="T">The type of the value being inserted.</typeparam>
        /// <param name="path">The path to the JSON attribute.</param>
        /// <param name="value">The value to insert.</param>
        /// <param name="createPath">True to create the path if it doesn't exist.</param>
        /// <param name="isXattr">true if the path is an xAttr; otherwise false.</param>
        /// <returns>A <see cref="MutateInSpecBuilder"/> for chaining.</returns>
        public MutateInSpecBuilder ArrayAddUnique<T>(string path, T value, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.ArrayAddUnique(path, value, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Performs an arithmetic increment or decrement on a numeric element within a document.
        /// </summary>
        /// <param name="path">The path to the element.</param>
        /// <param name="delta"> the amount to increase the value by</param>
        /// <param name="createPath"></param>
        /// <param name="isXattr"></param>
        /// <returns></returns>
        [Obsolete("Use the Increment overload which accepts an unsigned long.")]
        public MutateInSpecBuilder Increment(string path, long delta, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Increment(path, delta, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Performs an arithmetic increment or decrement on a numeric element within a document.
        /// </summary>
        /// <param name="path">The path to the element.</param>
        /// <param name="delta"> the amount to increase the value by</param>
        /// <param name="createPath"></param>
        /// <param name="isXattr"></param>
        /// <returns></returns>
        public MutateInSpecBuilder Increment(string path, ulong delta, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Increment(path, delta, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Performs an arithmetic increment or decrement on a numeric element within a document.
        /// </summary>
        /// <param name="path">The path to the element.</param>
        /// <param name="delta"> the amount to decrease the value by</param>
        /// <param name="createPath"></param>
        /// <param name="isXattr"></param>
        /// <returns></returns>
        [Obsolete("Use the Decrement overload which accepts an unsigned long. Negative signed long deltas may produce unexpected results.")]
        public MutateInSpecBuilder Decrement(string path, long delta, bool createPath = default(bool), bool isXattr = default(bool))
        {
            // delta must be negative
            if (delta > 0)
            {
                delta = -delta;
            }

            Specs.Add(MutateInSpec.Decrement(path, delta, createPath, isXattr));
            return this;
        }

        /// <summary>
        /// Performs an arithmetic increment or decrement on a numeric element within a document.
        /// </summary>
        /// <param name="path">The path to the element.</param>
        /// <param name="delta"> the amount to decrease the value by</param>
        /// <param name="createPath"></param>
        /// <param name="isXattr"></param>
        /// <returns></returns>
        public MutateInSpecBuilder Decrement(string path, ulong delta, bool createPath = default(bool), bool isXattr = default(bool))
        {
            Specs.Add(MutateInSpec.Decrement(path, delta, createPath, isXattr));
            return this;
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
