using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.ExpressionVisitors;
using Couchbase.Core.Serialization;

namespace Couchbase
{
    /// <summary>
    /// Extensions related to lambda path evaluation for <see cref="ILookupInBuilder{TDocument}"/>,
    /// <see cref="IMutateInBuilder{TDocument}"/>, and <see cref="IDocumentFragment{TDocument}"/>.
    /// </summary>
    public static class SubdocExtensions
    {
        #region LookupInBuilder

        /// <summary>
        /// Get a fragment of type <typeparamref name="TContent"/> from a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static ILookupInBuilder<TDocument> Get<TDocument, TContent>(this ILookupInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.Get(ParsePath(builder as ITypeSerializerProvider, path));
        }

        /// <summary>
        /// Check for existence of a fragment of type <typeparamref name="TContent"/> within a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static ILookupInBuilder<TDocument> Exists<TDocument, TContent>(this ILookupInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.Exists(ParsePath(builder as ITypeSerializerProvider, path));
        }

        /// <summary>
        /// Get the number of items in a fragment of type <typeparamref name="TContent"/> within a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static ILookupInBuilder<TDocument> GetCount<TDocument, TContent>(this ILookupInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.GetCount(ParsePath(builder as ITypeSerializerProvider, path));
        }

        #endregion

        #region MutateInBuilder

        /// <summary>
        /// Insert a fragment of type <typeparamref name="TContent"/> into a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <param name="value">Value to insert at <paramref cref="path"/>.</param>
        /// <param name="createParents">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> Insert<TDocument, TContent>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TContent value, bool createParents)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.Insert(ParsePath(builder as ITypeSerializerProvider, path), value, createParents);
        }

        /// <summary>
        /// Update or insert a fragment of type <typeparamref name="TContent"/> into a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <param name="value">Value to update or insert at <paramref name="path"/>.</param>
        /// <param name="createParents">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> Upsert<TDocument, TContent>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TContent value, bool createParents)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.Upsert(ParsePath(builder as ITypeSerializerProvider, path), value, createParents);
        }

        /// <summary>
        /// Replace a fragment of type <typeparamref name="TContent"/> in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <param name="value">Value to replace at <paramref name="path"/>.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> Replace<TDocument, TContent>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TContent value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.Replace(ParsePath(builder as ITypeSerializerProvider, path), value);
        }

        /// <summary>
        /// Remove a fragment of type <typeparamref name="TContent"/> from a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> Remove<TDocument, TContent>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.Remove(ParsePath(builder as ITypeSerializerProvider, path));
        }

        /// <summary>
        /// Push a fragment of type <typeparamref name="TContent"/> into the back of an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the array within the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being pushed.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array from the parent document.</param>
        /// <param name="value">Value to push into the array.</param>
        /// <param name="createParents">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> ArrayAppend<TDocument, TContent, TElement>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TElement value, bool createParents)
            where TContent : ICollection<TElement>
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.ArrayAppend(ParsePath(builder as ITypeSerializerProvider, path), value, createParents);
        }

        /// <summary>
        /// Push a fragment of type <typeparamref name="TContent"/> into the front of an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the array within the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being pushed.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array from the parent document.</param>
        /// <param name="value">Value to push into the array.</param>
        /// <param name="createParents">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> ArrayPrepend<TDocument, TContent, TElement>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TElement value, bool createParents)
            where TContent : ICollection<TElement>
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.ArrayPrepend(ParsePath(builder as ITypeSerializerProvider, path), value, createParents);
        }

        /// <summary>
        /// Insert a fragment of type <typeparamref name="TElement"/> into an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being inserted.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array element from the parent document.</param>
        /// <param name="value">Value to insert into the array.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> ArrayInsert<TDocument, TElement>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TElement>> path, TElement value)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.ArrayInsert(ParsePath(builder as ITypeSerializerProvider, path), value);
        }

        /// <summary>
        /// Add a unique fragment of type <typeparamref name="TContent"/> into an array in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the array within the parent document.</typeparam>
        /// <typeparam name="TElement">Type of the array element being added.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the array from the parent document.</param>
        /// <param name="value">Value to insert into the array.</param>
        /// <param name="createParents">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> ArrayAddUnique<TDocument, TContent, TElement>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, TElement value, bool createParents)
            where TContent : ICollection<TElement>
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.ArrayAddUnique(ParsePath(builder as ITypeSerializerProvider, path), value, createParents);
        }

        /// <summary>
        /// Increment or decrement a counter of type <typeparamref name="TContent"/> in a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="ILookupInBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the counter from the parent document.</param>
        /// <param name="delta">Amount to increment or decrement the counter.</param>
        /// <param name="createParents">If true, create parents along the path if they don't exist.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static IMutateInBuilder<TDocument> Counter<TDocument, TContent>(this IMutateInBuilder<TDocument> builder,
            Expression<Func<TDocument, TContent>> path, long delta, bool createParents)
        {
            if (builder == null)
            {
                throw new ArgumentNullException("builder");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return builder.Counter(ParsePath(builder as ITypeSerializerProvider, path), delta, createParents);
        }

        #endregion

        #region SubdocResult

        /// <summary>
        /// Get the result type <typeparamref name="TContent"/> from a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="result"><see cref="IDocumentFragment{TDocument}"/> where the the subdocument lookup was returned.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.
        /// This must be a path that was provided originally to the <see cref="ILookupInBuilder{TDocument}"/>.</param>
        /// <returns>The subdocument content.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="path"/> is null.</exception>
        public static TContent Content<TDocument, TContent>(this IDocumentFragment<TDocument> result, Expression<Func<TDocument, TContent>> path)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return result.Content<TContent>(ParsePath(result as ITypeSerializerProvider, path));
        }

        /// <summary>
        /// Get the existence result for a fragement of type <typeparamref name="TContent"/> from a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="result"><see cref="IDocumentFragment{TDocument}"/> where the the subdocument lookup was returned.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.
        /// This must be a path that was provided originally to the <see cref="ILookupInBuilder{TDocument}"/>.</param>
        /// <returns>True if the subdocument exists.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="path"/> is null.</exception>
        public static bool Exists<TDocument, TContent>(this IDocumentFragment<TDocument> result, Expression<Func<TDocument, TContent>> path)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }
            if (path == null)
            {
                throw new ArgumentNullException("path");
            }

            return result.Exists(ParsePath(result as ITypeSerializerProvider, path));
        }

        #endregion

        #region Helpers

        private static string ParsePath<TDocument, TContent>(ITypeSerializerProvider typeSerializerProvider, Expression<Func<TDocument, TContent>> path)
        {
            var generatedSerializer = typeSerializerProvider != null ? typeSerializerProvider.Serializer as IExtendedTypeSerializer : null;

            if (generatedSerializer == null)
            {
                throw new NotSupportedException("Serializer must be IExtendedTypeSerializer to support subdocument paths.");
            }

            return SubDocumentPathExpressionVisitor.GetPath(generatedSerializer, path);
        }

        #endregion
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
