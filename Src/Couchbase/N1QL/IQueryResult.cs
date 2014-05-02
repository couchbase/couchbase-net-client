using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.N1QL
{
    /// <summary>
    /// Interface for the results of a N1QL query.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IQueryResult<T>
    {
        /// <summary>
        /// The resultset of the N1QL query.
        /// </summary>
        List<T> Rows { get; set; }

        Error Error { get; set; }

        bool Success { get; }
    }
}
