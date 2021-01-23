using System;
using System.Linq.Expressions;
using Couchbase.KeyValue.ExpressionVisitors;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Extensions for <see cref="IMutateInResult"/>.
    /// </summary>
    public static class MutateInResultExtensions
    {
        /// <summary>
        /// Get the result type <typeparamref name="TContent"/> from a document of type <typeparamref name="TDocument"/>,
        /// using a given lambda expression path.
        /// </summary>
        /// <typeparam name="TDocument">Type of the parent document.</typeparam>
        /// <typeparam name="TContent">Type of the subdocument.</typeparam>
        /// <param name="result"><see cref="IMutateInResult{TDocument}"/> where the the subdocument mutation result was returned.</param>
        /// <param name="path">Lambda expression path that navigates to the subdocument from the parent document.
        /// This must be a path that was provided originally to the <see cref="MutateInSpecBuilder{TDocument}"/>.</param>
        /// <returns>The subdocument content.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="result"/> or <paramref name="path"/> is null.</exception>
        public static TContent ContentAs<TDocument, TContent>(this IMutateInResult<TDocument> result, Expression<Func<TDocument, TContent>> path)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (result == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(result));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            var pathString = SubDocumentPathExpressionVisitor.GetPath(result, path);
            var index = result.IndexOf(pathString);
            if (index < 0)
            {
                ThrowHelper.ThrowArgumentException(nameof(path), $"Path '{pathString}' is not found.");
            }

            return result.ContentAs<TContent>(index);
        }
    }
}
