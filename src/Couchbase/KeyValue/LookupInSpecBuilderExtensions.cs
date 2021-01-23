using System;
using System.Linq.Expressions;
using Couchbase.KeyValue.ExpressionVisitors;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Extensions for <see cref="LookupInSpecBuilder"/>.
    /// </summary>
    public static class LookupInSpecBuilderExtensions
    {
        /// <summary>
        /// Get a fragment of type <typeparamref name="TContent"/> from a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="LookupInSpecBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static LookupInSpecBuilder<TDocument> Get<TDocument, TContent>(this LookupInSpecBuilder<TDocument> builder,
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

            return (LookupInSpecBuilder<TDocument>) builder.Get(SubDocumentPathExpressionVisitor.GetPath(builder, path));
        }

        /// <summary>
        /// Check for existence of a fragment of type <typeparamref name="TContent"/> within a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="LookupInSpecBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static LookupInSpecBuilder<TDocument> Exists<TDocument, TContent>(this LookupInSpecBuilder<TDocument> builder,
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

            return (LookupInSpecBuilder<TDocument>) builder.Exists(SubDocumentPathExpressionVisitor.GetPath(builder, path));
        }

        /// <summary>
        /// Get the number of items in a fragment of type <typeparamref name="TContent"/> within a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="builder"><see cref="LookupInSpecBuilder{TDocument}"/> where the the subdocument lookup is being built.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.</param>
        /// <returns>The <paramref name="builder"/> for expression chaining.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="path"/> is null.</exception>
        public static LookupInSpecBuilder<TDocument> Count<TDocument, TContent>(this LookupInSpecBuilder<TDocument> builder,
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

            return (LookupInSpecBuilder<TDocument>) builder.Count(SubDocumentPathExpressionVisitor.GetPath(builder, path));
        }
    }
}
