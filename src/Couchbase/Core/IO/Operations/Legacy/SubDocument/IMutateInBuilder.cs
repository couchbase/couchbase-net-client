using System;
using Couchbase.KeyValue;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
{
    /// <summary>
    ///  Exposes the creation of a set of mutation operations to be performed.
    /// </summary>
    /// <typeparam name="TDocument">The strong typed document (POCO) reflecting the structure of the paths.</typeparam>
    public interface IMutateInBuilder<TDocument> : ISubDocBuilder<TDocument>
    {
        /// <summary>
        /// A "check-and-set" value for ensuring that a document has not been modified by another thread.
        /// </summary>
        ulong Cas { get; }

        /// <summary>
        /// The "time-to-live" or "TTL" that specifies the document's lifetime.
        /// </summary>
        TimeSpan Expiry { get; }

        /// <summary>
        /// A durability constraint ensuring that a document has been persisted to the n^th node.
        /// </summary>
        PersistTo PersistTo { get; }

        /// <summary>
        /// A durability constraint for ensuring that the document has been replicated to the n^th node.
        /// </summary>
        ReplicateTo ReplicateTo { get; }

        /// <summary>
        /// Inserts an element into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Insert(string path, object value, bool createParents = true);

        /// <summary>
        /// Inserts an element into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="pathFlags">The lookup flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Insert(string path, object value, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Inserts or updates an element within or into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Upsert(string path, object value, bool createParents = true);

        /// <summary>
        /// Inserts or updates an element within or into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Upsert(string path, object value, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Replaces an element or value within a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Replace(string path, object value);

        /// <summary>
        /// Replaces an element or value within a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Replace(string path, object value, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Removes an element or value from a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Remove(string path);

        /// <summary>
        /// Removes an element or value from a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Remove(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Inserts an array value at the end of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="value">An array value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(object value, bool createParents = true);

        /// <summary>
        /// Inserts one or more values at the end of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(bool createParents = false, params object[] values);

        /// <summary>
        /// Inserts a value to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An aray value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(string path, object value, bool createParents = true);

        /// <summary>
        /// Inserts a value to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An aray value.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(string path, object value, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Inserts one or more values to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(string path, bool createParents = false, params object[] values);

        /// <summary>
        /// Inserts one or more values to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags, params object[] values);

        /// <summary>
        /// Inserts a value to the beginning of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(object value, bool createParents = true);

        /// <summary>
        /// Inserts one or more values to the beginning of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(bool createParents = false, params object[] values);

        /// <summary>
        /// Inserts a value to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(string path, object value, bool createParents = true);

        /// <summary>
        /// Inserts a value to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(string path, object value, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Inserts one or more values to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(string path, bool createParents = false, params object[] values);

        /// <summary>
        /// Inserts one or more values to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags, params object[] values);

        /// <summary>
        /// Inserts a value at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A value.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayInsert(string path, object value);

        /// <summary>
        /// Inserts a value at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A value.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayInsert(string path, object value, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Inserts one or more values at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayInsert(string path, params object[] values);

        /// <summary>
        /// Inserts one or more values at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayInsert(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags, params object[] values);

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array at the root of the JSON document.
        /// </summary>
        /// <param name="value">A unique value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAddUnique(object value, bool createParents = true);

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A unique value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAddUnique(string path, object value, bool createParents = true);

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A unique value.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAddUnique(string path, object value, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Performs an arithmetic increment or decrement operation on a numeric value in a document.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="delta">The value to increment or decrement the original value by.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is true.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Counter(string path, long delta, bool createParents = true);

        /// <summary>
        /// Performs an arithmetic increment or decrement operation on a numeric value in a document.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="delta">The value to increment or decrement the original value by.</param>
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Counter(string path, long delta, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// Applies an expiration to a document.
        /// </summary>
        /// <param name="expiry">The "time-to-live" or TTL of the document.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> WithExpiry(TimeSpan expiry);

        /// <summary>
        /// A "check-and-set" value for ensuring that a document has not been modified by another thread.
        /// </summary>
        /// <param name="cas">The CAS value.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> WithCas(long cas);

        /// <summary>
        /// A "check-and-set" value for ensuring that a document has not been modified by another thread.
        /// </summary>
        /// <param name="cas">The CAS value.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> WithCas(ulong cas);

        /// <summary>
        /// A durability constraint ensuring that a document has been persisted to the n^th node.
        /// </summary>
        /// <param name="persistTo">The <see cref="PersistTo"/> value.</param>
        /// <returns></returns>
        IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo);

        /// <summary>
        /// A durability constraint ensuring that a document has been persisted to the n^th node.
        /// </summary>
        /// <param name="replicateTo">The <see cref="ReplicateTo"/> value.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> WithDurability(ReplicateTo replicateTo);

        /// <summary>
        /// Sets the <see cref="ReplicateTo"/> and <see cref="PersistTo"/> values for a document.
        /// </summary>
        /// <param name="persistTo">The <see cref="PersistTo"/> value.</param>
        /// <param name="replicateTo">The <see cref="ReplicateTo"/> value.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo, ReplicateTo replicateTo);

        /// <summary>
        /// The maximum time allowed for an operation to live before timing out.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> WithTimeout(TimeSpan timeout);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
