using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    internal interface IPreparable
    {
        /// <summary>
        /// Returns true if a prepared statement is cached for an IQueryRequest instance.
        /// </summary>
        bool HasPrepared { get; }

        /// <summary>
        ///  Sets a N1QL prepared statement to be executed.
        /// </summary>
        /// <param name="preparedStatement">The prepared form of the N1QL statement to be executed. </param>
        /// <returns>A reference to the current <see cref="IQueryRequest"/> for method chaining.</returns>
        /// <remarks>If both prepared and statement are present and non-empty, an error is returned.</remarks>
        /// <remarks>Required if prepared not provided.</remarks>
        void CachePreparedStatement(string preparedStatement);

        /// <summary>
        /// Clears all cached prepared statements
        /// </summary>
        void ClearCache();
    }
}
