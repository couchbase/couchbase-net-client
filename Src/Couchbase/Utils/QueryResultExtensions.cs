using System.Text;
using Couchbase.N1QL;

namespace Couchbase.Utils
{
    /// <summary>
    /// Provides methods which extend the <see cref="IQueryResult{T}"/> interface, without actually modifying the interface.
    /// </summary>
    internal static class QueryResultExtensions
    {
        /// <summary>
        /// Converts the <see cref="IQueryResult{T}.Errors"/> collection to a string representation.
        /// </summary>
        /// <typeparam name="T">The Type of the row body.</typeparam>
        /// <param name="query">The <see cref="IQueryResult{T}"/> reference.</param>
        /// <returns></returns>
        public static string GetErrorsAsString<T>(this IQueryResult<T> query)
        {
            var builder = new StringBuilder();
            foreach (var error in query.Errors)
            {
                builder.AppendFormat("{0} - {1}, ", error.Code, error.Message);
            }
            return builder.ToString();
        }
    }
}