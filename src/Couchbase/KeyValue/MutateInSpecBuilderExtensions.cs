using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Couchbase.KeyValue.ExpressionVisitors;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Extensions for <see cref="MutateInSpecBuilder{TDocument}"/>.
    /// </summary>
    public static class MutateInSpecBuilderExtensions
    {
        /// <summary>
        /// Insert a fragment of type <typeparamref name="TContent"/> into a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <param name="value">Value to insert at path.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> Insert<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TContent value, bool createPath = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Insert(SubDocumentPathExpressionVisitor.GetPath(builder, path), value, createPath);
        }

        /// <summary>
        /// Update or insert a fragment of type <typeparamref name="TContent"/> into a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <param name="value">Value to update or insert at <paramref name="path"/>.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> Upsert<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TContent value, bool createPath = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Upsert(SubDocumentPathExpressionVisitor.GetPath(builder, path), value, createPath);
        }

        /// <summary>
        /// Replace a fragment of type <typeparamref name="TContent"/> in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <param name="value">Value to replace at <paramref name="path"/>.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> Replace<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TContent value)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Replace(SubDocumentPathExpressionVisitor.GetPath(builder, path), value);
        }

        /// <summary>
        /// Remove a fragment of type <typeparamref name="TContent"/> from a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> Remove<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Remove(SubDocumentPathExpressionVisitor.GetPath(builder, path));
        }

        /// <summary>
        /// Push a fragment of type <typeparamref name="TContent"/> into the back of an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the array within the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being pushed.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array from the parent document.</param>
        /// <param name="value">Value to push into the array.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> ArrayAppend<TDocument, TContent, TElement>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TElement value, bool createPath = false)
            where TContent : ICollection<TElement>
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.ArrayAppend(SubDocumentPathExpressionVisitor.GetPath(builder, path), value, createPath);
        }

        /// <summary>
        /// Push a fragment of type <typeparamref name="TContent"/> into the front of an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the array within the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being pushed.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array from the parent document.</param>
        /// <param name="value">Value to push into the array.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> ArrayPrepend<TDocument, TContent, TElement>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TElement value, bool createPath = false)
            where TContent : ICollection<TElement>
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.ArrayPrepend(SubDocumentPathExpressionVisitor.GetPath(builder, path), value, createPath);
        }

        /// <summary>
        /// Insert a fragment of type <typeparamref name="TElement"/> into an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being inserted.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array element from the parent document.</param>
        /// <param name="value">Value to insert into the array.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> ArrayInsert<TDocument, TElement>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TElement>> path, TElement value)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.ArrayInsert(SubDocumentPathExpressionVisitor.GetPath(builder, path), value);
        }

        /// <summary>
        /// Add a unique fragment of type <typeparamref name="TContent"/> into an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the array within the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being added.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array from the parent document.</param>
        /// <param name="value">Value to insert into the array.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> ArrayAddUnique<TDocument, TContent, TElement>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TElement value, bool createPath = false)
            where TContent : ICollection<TElement>
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.ArrayAddUnique(SubDocumentPathExpressionVisitor.GetPath(builder, path), value, createPath);
        }

        /// <summary>
        /// Increment a counter of type <typeparamref name="TContent"/> in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the counter from the parent document.</param>
        /// <param name="delta">Amount to increment the counter.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        [Obsolete("Use the Increment overload which accepts an unsigned long.")]
        public static MutateInSpecBuilder<TDocument> Increment<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, long delta, bool createPath = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Increment(SubDocumentPathExpressionVisitor.GetPath(builder, path), delta, createPath);
        }

        /// <summary>
        /// Increment a counter of type <typeparamref name="TContent"/> in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the counter from the parent document.</param>
        /// <param name="delta">Amount to increment the counter.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> Increment<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, ulong delta, bool createPath = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Increment(SubDocumentPathExpressionVisitor.GetPath(builder, path), delta, createPath);
        }

        /// <summary>
        /// Decrement a counter of type <typeparamref name="TContent"/> in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the counter from the parent document.</param>
        /// <param name="delta">Amount to decrement the counter.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        [Obsolete("Use the Decrement overload which accepts an unsigned long. Negative signed long deltas may produce unexpected results.")]
        public static MutateInSpecBuilder<TDocument> Decrement<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, long delta, bool createPath = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Decrement(SubDocumentPathExpressionVisitor.GetPath(builder, path), delta, createPath);
        }

        /// <summary>
        /// Decrement a counter of type <typeparamref name="TContent"/> in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="MutateInSpecBuilder{TDocument}"/> where the the subdocument mutation is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the counter from the parent document.</param>
        /// <param name="delta">Amount to decrement the counter.</param>
        /// <param name="createPath">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static MutateInSpecBuilder<TDocument> Decrement<TDocument, TContent>(this MutateInSpecBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, ulong delta, bool createPath = false)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            return (MutateInSpecBuilder<TDocument>)
                builder.Decrement(SubDocumentPathExpressionVisitor.GetPath(builder, path), delta, createPath);
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
