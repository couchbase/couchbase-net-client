using System;

namespace Couchbase.Core
{
    /// <summary>
    ///  Exposes the creation of a set of mutation operations to be performed.
    /// </summary>
    /// <typeparam name="TDocument">The strong typed document (POCO) reflecting the structure of the paths.</typeparam>
    public interface IMutateInBuilder<out TDocument> : ISubDocBuilder<TDocument>
    {
        /// <summary>
        /// A "check-and-set" value for ensuring that a document has not been modified by another thread.
        /// </summary>
        long Cas { get; }

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
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Insert(string path, object value, bool createParents = false);

        /// <summary>
        /// Inserts or updates an element within or into a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Upsert(string path, object value, bool createParents = false);

        /// <summary>
        /// Replaces an element or value within a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Replace(string path, object value);

        /// <summary>
        /// Removes an element or value from a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Remove(string path);

        /// <summary>
        /// Inserts an array value at the end of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="value">An array value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(object value, bool createParents = false);

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
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(string path, object value, bool createParents = false);

        /// <summary>
        /// Inserts one or more values to the end of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAppend(string path, bool createParents = false, params object[] values);

        /// <summary>
        /// Inserts a value to the beginning of an array that is the root of a JSON document.
        /// </summary>
        /// <param name="value">An array value, dictionary entry, scalar or any other valid JSON item.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(object value, bool createParents = false);

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
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(string path, object value, bool createParents = false);

        /// <summary>
        /// Inserts one or more values to the beginning of an array in a JSON document at a given path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayPrepend(string path, bool createParents = false, params object[] values);

        /// <summary>
        /// Inserts a value at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A value.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayInsert(string path, object value);

        /// <summary>
        /// Inserts one or more values at a given position within an array. The position is indicated as part of the path.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="values">One or more values.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayInsert(string path, params object[] values);

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array at the root of the JSON document.
        /// </summary>
        /// <param name="value">A unique value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAddUnique(object value, bool createParents = false);

        /// <summary>
        /// Adds a value to an array if the value does not already exist in the array.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="value">A unique value.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> ArrayAddUnique(string path, object value, bool createParents = false);

        /// <summary>
        /// Performs an arithmetic increment or decrement operation on a numeric value in a document.
        /// </summary>
        /// <param name="path">A string (N1QL syntax) used to specify a location within the document.</param>
        /// <param name="delta">The value to increment or decrement the original value by.</param>
        /// <param name="createParents">If <s>true</s>, the parent will be added to the document. The default is false.</param>
        /// <returns>An <see cref="IMutateInBuilder{TDocument}"/> reference for chaining operations.</returns>
        IMutateInBuilder<TDocument> Counter(string path, long delta, bool createParents = false);

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
    }
}
